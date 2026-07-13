using System.ComponentModel.DataAnnotations;

namespace AccountItERP.Models
{
    public class CustomerVendor
    {
        [Key]
        public int CustomerVendorID { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [Required]
        [StringLength(25)]
        public string Type { get; set; } = "";

        [StringLength(15)]
        public string ContactNumber { get; set; } = "";

        [StringLength(100)]
        public string Email { get; set; } = "";

        [StringLength(150)]
        public string Address { get; set; } = "";
    }
}