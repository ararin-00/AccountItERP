using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountItERP.Models
{
    public class Income
    {
        [Key]
        public int IncomeID { get; set; }

        public int UserID { get; set; }

        [Required]
        [StringLength(50)]
        public string IncomeSource { get; set; } = "";

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime DateReceived { get; set; }

        [StringLength(100)]
        public string Description { get; set; } = "";
    }
}