using AccountItERP.Data;
using AccountItERP.Models;
using AccountItERP.Helpers;
using AccountItERP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public PaymentController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool CanAccessPayment()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Accountant", "Staff");
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            if (!CanAccessPayment())
                return RedirectToAction("AccessDenied", "Account");

            int pageSize = 10;

            var query = _context.Payments
                .Include(p => p.Invoice)
                .Include(p => p.CustomerVendor)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(p =>
                    (p.Invoice != null &&
                    p.Invoice.InvoiceNumber.Contains(search)) ||
                    (p.CustomerVendor != null &&
                    p.CustomerVendor.Name.Contains(search)));
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var payments = await query
                .OrderByDescending(p => p.PaymentDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;


            ViewBag.AvailableInvoices = await _context.Invoices
                .Include(i => i.CustomerVendor)
                .Where(i => i.Status != "Paid")
                .OrderBy(i => i.InvoiceNumber)
                .ToListAsync();

            ViewBag.AllInvoices = await _context.Invoices
                .Include(i => i.CustomerVendor)
                .OrderBy(i => i.InvoiceNumber)
                .ToListAsync();

            ViewBag.InvoicePaidTotals = await _context.Payments
            .GroupBy(p => p.InvoiceID)
            .Select(g => new
            {
                InvoiceID = g.Key,
                TotalPaid = g.Sum(p => p.AmountPaid)
            })
            .ToDictionaryAsync(x => x.InvoiceID, x => x.TotalPaid);

        ViewBag.InstallmentPaidCounts = await _context.Payments
            .Where(p => p.InstallmentNumber.HasValue)
            .GroupBy(p => p.InvoiceID)
            .Select(g => new
            {
                InvoiceID = g.Key,
                PaidMonths = g
                    .Select(p => p.InstallmentNumber)
                    .Distinct()
                    .Count()
            })
            .ToDictionaryAsync(x => x.InvoiceID, x => x.PaidMonths);

        return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payment payment)
        {
            if (!CanAccessPayment())
                return RedirectToAction("AccessDenied", "Account");

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceID == payment.InvoiceID);

            if (invoice == null)
            {
                TempData["Error"] = "Selected invoice was not found.";
                return RedirectToAction(nameof(Index));
            }

            if (invoice.Status == "Paid")
            {
                TempData["Error"] = "This invoice is already fully paid.";
                return RedirectToAction(nameof(Index));
            }

            if (payment.AmountPaid <= 0)
            {
                TempData["Error"] = "Payment amount must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            var totalPaid = await _context.Payments
                .Where(p => p.InvoiceID == invoice.InvoiceID)
                .SumAsync(p => p.AmountPaid);

            var remainingBalance = invoice.TotalAmount - totalPaid;

            if (remainingBalance <= 0)
            {
                TempData["Error"] = "This invoice has no remaining balance.";
                return RedirectToAction(nameof(Index));
            }

            if (payment.AmountPaid > remainingBalance)
            {
                TempData["Error"] =
                    $"Payment cannot exceed the remaining balance of ₱{remainingBalance:N2}.";

                return RedirectToAction(nameof(Index));
            }

            if (invoice.IsInstallment)
            {
                if (!invoice.InstallmentMonths.HasValue ||
                    invoice.InstallmentMonths.Value <= 0)
                {
                    TempData["Error"] =
                        "This invoice does not have a valid installment plan.";

                    return RedirectToAction(nameof(Index));
                }

                var lastInstallmentNumber = await _context.Payments
                    .Where(p =>
                        p.InvoiceID == invoice.InvoiceID &&
                        p.InstallmentNumber.HasValue)
                    .MaxAsync(p => (int?)p.InstallmentNumber);

                var nextInstallmentNumber =
                    (lastInstallmentNumber ?? 0) + 1;

                if (nextInstallmentNumber > invoice.InstallmentMonths.Value)
                {
                    TempData["Error"] =
                        "All installments for this invoice have already been recorded.";

                    return RedirectToAction(nameof(Index));
                }

                var standardInstallmentAmount = Math.Round(
                    invoice.TotalAmount / invoice.InstallmentMonths.Value,
                    2);

                var expectedAmount =
                    nextInstallmentNumber == invoice.InstallmentMonths.Value
                        ? remainingBalance
                        : Math.Min(standardInstallmentAmount, remainingBalance);

                if (Math.Round(payment.AmountPaid, 2) != expectedAmount)
                {
                    TempData["Error"] =
                        $"Installment {nextInstallmentNumber} of " +
                        $"{invoice.InstallmentMonths.Value} must be ₱{expectedAmount:N2}.";

                    return RedirectToAction(nameof(Index));
                }

                // Ignore any installment number submitted by the browser.
                // The server determines the correct sequence.
                payment.InstallmentNumber = nextInstallmentNumber;
            }
            else
            {
                payment.InstallmentNumber = null;
            }

            payment.CustomerVendorID = invoice.CustomerVendorID;

            ModelState.Remove("CustomerVendorID");
            ModelState.Remove("CustomerVendor");
            ModelState.Remove("InstallmentNumber");

            if (!ModelState.IsValid)
            {
                TempData["Error"] =
                    "Please complete all required payment fields.";

                return RedirectToAction(nameof(Index));
            }

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            await UpdateInvoiceStatusAsync(payment.InvoiceID);

            var installmentDescription = invoice.IsInstallment
                ? $" as installment {payment.InstallmentNumber} of {invoice.InstallmentMonths}"
                : "";

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Payment",
                "Create",
                payment.PaymentID,
                $"Created payment of ₱{payment.AmountPaid:N2}{installmentDescription} " +
                $"for invoice '{invoice.InvoiceNumber}'."
            );

            TempData["Success"] = invoice.IsInstallment
                ? $"Installment {payment.InstallmentNumber} of {invoice.InstallmentMonths} recorded successfully."
                : "Payment record added successfully.";

            return RedirectToAction(nameof(Index));
        }








        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Payment payment)
        {
            if (!CanAccessPayment())
                return RedirectToAction("AccessDenied", "Account");

            var oldPayment = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PaymentID == payment.PaymentID);

            if (oldPayment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(Index));
            }

            var invoice = await _context.Invoices.FindAsync(payment.InvoiceID);

            if (invoice == null)
            {
                TempData["Error"] = "Selected invoice was not found.";
                return RedirectToAction(nameof(Index));
            }

            if (payment.AmountPaid <= 0)
            {
                TempData["Error"] = "Payment amount must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            var otherPaymentsTotal = await _context.Payments
                .Where(p =>
                    p.InvoiceID == payment.InvoiceID &&
                    p.PaymentID != payment.PaymentID)
                .SumAsync(p => p.AmountPaid);

            var availableBalance = invoice.TotalAmount - otherPaymentsTotal;

            if (payment.AmountPaid > availableBalance)
            {
                TempData["Error"] =
                    $"Payment cannot exceed the available balance of ₱{availableBalance:N2}.";

                return RedirectToAction(nameof(Index));
            }

            payment.CustomerVendorID = invoice.CustomerVendorID;

            ModelState.Remove("CustomerVendorID");
            ModelState.Remove("CustomerVendor");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required payment fields.";
                return RedirectToAction(nameof(Index));
            }

            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();

            await UpdateInvoiceStatusAsync(payment.InvoiceID);

            if (oldPayment.InvoiceID != payment.InvoiceID)
            {
                await UpdateInvoiceStatusAsync(oldPayment.InvoiceID);
            }

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Payment",
                "Update",
                payment.PaymentID,
                $"Updated payment to ₱{payment.AmountPaid:N2} for invoice '{invoice.InvoiceNumber}'."
            );

            TempData["Success"] = "Payment record updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanAccessPayment())
                return RedirectToAction("AccessDenied", "Account");

            var payment = await _context.Payments
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(p => p.PaymentID == id);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(Index));
            }

            int invoiceId = payment.InvoiceID;
            int recordId = payment.PaymentID;
            decimal amount = payment.AmountPaid;
            string invoiceNumber = payment.Invoice?.InvoiceNumber ?? "Unknown Invoice";

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            await UpdateInvoiceStatusAsync(invoiceId);

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Payment",
                "Delete",
                recordId,
                $"Deleted payment of ₱{amount:N2} for invoice '{invoiceNumber}'."
            );

            TempData["Success"] = "Payment record deleted successfully.";
            return RedirectToAction(nameof(Index));
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