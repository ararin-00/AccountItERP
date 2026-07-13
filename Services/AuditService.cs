using AccountItERP.Data;
using AccountItERP.Models;

namespace AccountItERP.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            int userId,
            string module,
            string action,
            int? recordId,
            string description)
        {
            var auditLog = new AuditLog
            {
                UserID = userId,
                Module = module,
                Action = action,
                RecordID = recordId,
                Description = description,
                CreatedAt = DateTime.Now
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}