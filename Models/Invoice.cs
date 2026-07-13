using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountItERP.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceID { get; set; }

        public int CustomerVendorID { get; set; }

        [Required]
        [StringLength(25)]
        public string InvoiceNumber { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        public bool IsInstallment { get; set; } = false;

        public int? InstallmentMonths { get; set; }
        
        [ForeignKey("CustomerVendorID")]
        public CustomerVendor? CustomerVendor { get; set; }
    }
}