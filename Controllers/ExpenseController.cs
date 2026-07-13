using AccountItERP.Data;
using AccountItERP.Models;
using AccountItERP.Helpers;
using AccountItERP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class ExpenseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public ExpenseController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool CanAccessExpense()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Accountant");
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            if (!CanAccessExpense())
                return RedirectToAction("AccessDenied", "Account");

            int pageSize = 10;

            var query = _context.Expenses.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(e => e.ExpenseCategory.Contains(search));
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var expenses = await query
                .OrderByDescending(e => e.ExpenseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(expenses);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Expense expense)
        {
            if (!CanAccessExpense())
                return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required expense fields.";
                return RedirectToAction(nameof(Index));
            }

            expense.UserID = HttpContext.Session.GetInt32("UserID") ?? 1;

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                expense.UserID,
                "Expense",
                "Create",
                expense.ExpenseID,
                $"Created expense record '{expense.ExpenseCategory}' worth ₱{expense.Amount:N2}."
            );

            TempData["Success"] = "Expense record added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Expense expense)
        {
            if (!CanAccessExpense())
                return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required expense fields.";
                return RedirectToAction(nameof(Index));
            }

            expense.UserID = HttpContext.Session.GetInt32("UserID") ?? 1;

            _context.Expenses.Update(expense);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                expense.UserID,
                "Expense",
                "Update",
                expense.ExpenseID,
                $"Updated expense record '{expense.ExpenseCategory}' worth ₱{expense.Amount:N2}."
            );

            TempData["Success"] = "Expense record updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanAccessExpense())
                return RedirectToAction("AccessDenied", "Account");

            var expense = await _context.Expenses.FindAsync(id);

            if (expense == null)
            {
                TempData["Error"] = "Expense record not found.";
                return RedirectToAction(nameof(Index));
            }

            var userId = HttpContext.Session.GetInt32("UserID") ?? expense.UserID;
            var category = expense.ExpenseCategory;
            var amount = expense.Amount;
            var recordId = expense.ExpenseID;

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Expense",
                "Delete",
                recordId,
                $"Deleted expense record '{category}' worth ₱{amount:N2}."
            );

            TempData["Success"] = "Expense record deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}