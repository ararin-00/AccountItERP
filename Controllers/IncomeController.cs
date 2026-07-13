using AccountItERP.Data;
using AccountItERP.Models;
using AccountItERP.Services;
using AccountItERP.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class IncomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public IncomeController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool CanAccessIncome()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Accountant");
        }

        public async Task<IActionResult> Index(string? search, DateTime? dateFilter, int page = 1)
        {
            if (!CanAccessIncome())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            int pageSize = 10;

            var query = _context.Incomes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(i =>
                    i.IncomeSource.Contains(search));
            }


            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var incomes = await query
                .OrderByDescending(i => i.DateReceived)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.DateFilter = dateFilter?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(incomes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Income income)
        {
            if (!CanAccessIncome())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required income fields.";
                return RedirectToAction(nameof(Index));
            }

            income.UserID = HttpContext.Session.GetInt32("UserID") ?? 1;

            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                income.UserID,
                "Income",
                "Create",
                income.IncomeID,
                $"Created income record '{income.IncomeSource}' worth ₱{income.Amount:N2}."
            );

            TempData["Success"] = "Income record added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Income income)
        {
            if (!CanAccessIncome())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required income fields.";
                return RedirectToAction(nameof(Index));
            }

            income.UserID = HttpContext.Session.GetInt32("UserID") ?? 1;

            _context.Incomes.Update(income);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                income.UserID,
                "Income",
                "Update",
                income.IncomeID,
                $"Updated income record '{income.IncomeSource}' worth ₱{income.Amount:N2}."
            );

            TempData["Success"] = "Income record updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanAccessIncome())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var income = await _context.Incomes.FindAsync(id);

            if (income == null)
            {
                TempData["Error"] = "Income record not found.";
                return RedirectToAction(nameof(Index));
            }

            var userId = HttpContext.Session.GetInt32("UserID") ?? income.UserID;
            var incomeSource = income.IncomeSource;
            var amount = income.Amount;
            var recordId = income.IncomeID;

            _context.Incomes.Remove(income);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Income",
                "Delete",
                recordId,
                $"Deleted income record '{incomeSource}' worth ₱{amount:N2}."
            );

            TempData["Success"] = "Income record deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}