using AccountItERP.Data;
using AccountItERP.Helpers;
using AccountItERP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class UserManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public UserManagementController(ApplicationDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool CanAccessUserManagement()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator");
        }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            if (!CanAccessUserManagement())
                return RedirectToAction("AccessDenied", "Account");

            int pageSize = 10;

            var usersQuery = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                usersQuery = usersQuery.Where(u =>
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search) ||
                    u.Username.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.Role.Contains(search) ||
                    u.Status.Contains(search));
            }

            int totalUsers = await usersQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var users = await usersQuery
                .OrderBy(u => u.LastName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(int id, string role, string status)
        {
            if (!CanAccessUserManagement())
                return RedirectToAction("AccessDenied", "Account");

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                TempData["Error"] = "User account not found.";
                return RedirectToAction(nameof(Index));
            }

            var oldRole = user.Role;
            var oldStatus = user.Status;

            user.Role = role;
            user.Status = status;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                HttpContext.Session.GetInt32("UserID") ?? 1,
                "User Management",
                "Update",
                user.UserID,
                $"Updated user '{user.Username}' from Role: {oldRole}, Status: {oldStatus} to Role: {role}, Status: {status}."
            );

            TempData["Success"] = "User role and status updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}