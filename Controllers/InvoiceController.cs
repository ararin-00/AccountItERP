using AccountItERP.Data;
using AccountItERP.Models;
using AccountItERP.Helpers;
using AccountItERP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public InvoiceController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool CanAccessInvoice()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Accountant");
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            if (!CanAccessInvoice())
                return RedirectToAction("AccessDenied", "Account");

            int pageSize = 10;

            var query = _context.Invoices
                .Include(i => i.CustomerVendor)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(i =>
                    i.InvoiceNumber.Contains(search) ||
                    i.Status.Contains(search) ||
                    i.CustomerVendor!.Name.Contains(search));
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            ViewBag.CustomerVendors = await _context.CustomerVendors
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.InvoicePaidTotals = await _context.Payments
                .GroupBy(p => p.InvoiceID)
                .Select(g => new
                {
                    InvoiceID = g.Key,
                    TotalPaid = g.Sum(x => x.AmountPaid)
                })
                .ToDictionaryAsync(x => x.InvoiceID, x => x.TotalPaid);


            return View(invoices);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice invoice)
        {
            if (!CanAccessInvoice())
                return RedirectToAction("AccessDenied", "Account");

            invoice.InvoiceNumber = await GenerateInvoiceNumberAsync();
            invoice.Status = "Pending";

            ModelState.Remove("InvoiceNumber");
            ModelState.Remove("Status");

            if (!invoice.IsInstallment)
            {
                invoice.InstallmentMonths = null;
            }
            else if (!invoice.InstallmentMonths.HasValue ||
                    invoice.InstallmentMonths.Value <= 0)
            {
                TempData["Error"] =
                    "Please select the number of installment months.";

                return RedirectToAction(nameof(Index));
            }


            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required invoice fields.";
                return RedirectToAction(nameof(Index));
            }

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Invoice",
                "Create",
                invoice.InvoiceID,
                $"Created invoice '{invoice.InvoiceNumber}' worth ₱{invoice.TotalAmount:N2}."
            );

            TempData["Success"] = $"Invoice {invoice.InvoiceNumber} created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Invoice invoice)
        {
            if (!CanAccessInvoice())
                return RedirectToAction("AccessDenied", "Account");

            var existingInvoice = await _context.Invoices.FindAsync(invoice.InvoiceID);

            if (existingInvoice == null)
            {
                TempData["Error"] = "Invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            if (invoice.IsInstallment &&
                (!invoice.InstallmentMonths.HasValue ||
                invoice.InstallmentMonths.Value <= 0))
            {
                TempData["Error"] = "Please select the number of installment months.";
                return RedirectToAction(nameof(Index));
            }

            existingInvoice.CustomerVendorID = invoice.CustomerVendorID;
            existingInvoice.InvoiceDate = invoice.InvoiceDate;
            existingInvoice.DueDate = invoice.DueDate;
            existingInvoice.TotalAmount = invoice.TotalAmount;
            existingInvoice.IsInstallment = invoice.IsInstallment;
            existingInvoice.InstallmentMonths =
                invoice.IsInstallment
                    ? invoice.InstallmentMonths
                    : null;

            await _context.SaveChangesAsync();

            await UpdateInvoiceStatusAsync(existingInvoice.InvoiceID);

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Invoice",
                "Update",
                existingInvoice.InvoiceID,
                $"Updated invoice '{existingInvoice.InvoiceNumber}' worth ₱{existingInvoice.TotalAmount:N2}."
            );

            TempData["Success"] = $"Invoice {existingInvoice.InvoiceNumber} updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanAccessInvoice())
                return RedirectToAction("AccessDenied", "Account");

            var invoice = await _context.Invoices.FindAsync(id);

            if (invoice == null)
            {
                TempData["Error"] = "Invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            var invoiceNumber = invoice.InvoiceNumber;
            var amount = invoice.TotalAmount;
            var recordId = invoice.InvoiceID;

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Invoice",
                "Delete",
                recordId,
                $"Deleted invoice '{invoiceNumber}' worth ₱{amount:N2}."
            );

            TempData["Success"] = $"Invoice {invoiceNumber} deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            int year = DateTime.Now.Year;
            string prefix = $"INV-{year}-";

            var latestInvoiceNumber = await _context.Invoices
                .Where(i => i.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(i => i.InvoiceNumber)
                .Select(i => i.InvoiceNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrEmpty(latestInvoiceNumber))
            {
                string numberPart = latestInvoiceNumber.Replace(prefix, "");

                if (int.TryParse(numberPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            return $"{prefix}{nextNumber:D6}";
        }

        private async Task UpdateInvoiceStatusAsync(int invoiceId)
        {
            var invoice = await _context.Invoices.FindAsync(invoiceId);

            if (invoice == null)
                return;

            var totalPaid = await _context.Payments
                .Where(p => p.InvoiceID == invoiceId)
                .SumAsync(p => p.AmountPaid);

            invoice.Status = totalPaid >= invoice.TotalAmount ? "Paid" : "Pending";

            await _context.SaveChangesAsync();
        }
    }
}