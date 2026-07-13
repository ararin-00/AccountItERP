using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountItERP.Models
{
    public class Expense
    {
        [Key]
        public int ExpenseID { get; set; }

        public int UserID { get; set; }

        [Required]
        [StringLength(50)]
        public string ExpenseCategory { get; set; } = "";

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; }

        [StringLength(100)]
        public string Description { get; set; } = "";
    }
}