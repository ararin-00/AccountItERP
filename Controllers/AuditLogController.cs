using AccountItERP.Data;
using AccountItERP.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CanAccessAuditLogs()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Auditor");
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            if (!CanAccessAuditLogs())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            int pageSize = 10;

            var query = _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt)
                .AsQueryable();

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(logs);
        }
    }
}