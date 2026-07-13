using AccountItERP.Data;
using AccountItERP.Models;
using AccountItERP.Helpers;
using AccountItERP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class CustomerVendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public CustomerVendorController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool CanAccessCustomerVendor()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Accountant");
        }

        public async Task<IActionResult> Index(string? search, string? typeFilter, int page = 1)
        {
            if (!CanAccessCustomerVendor())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            int pageSize = 10;

            var query = _context.CustomerVendors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => c.Name.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(c => c.Type == typeFilter);
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var records = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.TypeFilter = typeFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(records);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerVendor customerVendor)
        {
            if (!CanAccessCustomerVendor())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required customer/vendor fields.";
                return RedirectToAction(nameof(Index));
            }

            _context.CustomerVendors.Add(customerVendor);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Customer/Vendor",
                "Create",
                customerVendor.CustomerVendorID,
                $"Created {customerVendor.Type} '{customerVendor.Name}'."
            );

            TempData["Success"] = "Customer/vendor record added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerVendor customerVendor)
        {
            if (!CanAccessCustomerVendor())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required customer/vendor fields.";
                return RedirectToAction(nameof(Index));
            }

            _context.CustomerVendors.Update(customerVendor);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Customer/Vendor",
                "Update",
                customerVendor.CustomerVendorID,
                $"Updated {customerVendor.Type} '{customerVendor.Name}'."
            );

            TempData["Success"] = "Customer/vendor record updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanAccessCustomerVendor())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var customerVendor = await _context.CustomerVendors.FindAsync(id);

            if (customerVendor == null)
            {
                TempData["Error"] = "Customer/vendor record not found.";
                return RedirectToAction(nameof(Index));
            }

            var name = customerVendor.Name;
            var type = customerVendor.Type;
            var recordId = customerVendor.CustomerVendorID;

            _context.CustomerVendors.Remove(customerVendor);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "Customer/Vendor",
                "Delete",
                recordId,
                $"Deleted {type} '{name}'."
            );

            TempData["Success"] = "Customer/vendor record deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}