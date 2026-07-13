using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountItERP.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        public int InvoiceID { get; set; }

        public int CustomerVendorID { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AmountPaid { get; set; }

        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }

        [StringLength(30)]
        public string PaymentMethod { get; set; } = "";

        public int? InstallmentNumber { get; set; }

        [ForeignKey("InvoiceID")]
        public Invoice? Invoice { get; set; }

        [ForeignKey("CustomerVendorID")]
        public CustomerVendor? CustomerVendor { get; set; }
    }
}