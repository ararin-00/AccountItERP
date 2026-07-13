using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountItERP.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogID { get; set; }

        public int UserID { get; set; }

        [Required]
        [StringLength(30)]
        public string Module { get; set; } = "";

        [Required]
        [StringLength(20)]
        public string Action { get; set; } = "";

        public int? RecordID { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(UserID))]
        public User? User { get; set; }
    }
}