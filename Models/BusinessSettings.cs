using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceSite.Models
{
    public class BusinessSettings
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string BusinessName { get; set; } = "Local Pork Delivery";

        [StringLength(30)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(30)]
        public string WhatsApp { get; set; } = string.Empty;

        [StringLength(250)]
        public string ServiceArea { get; set; } = string.Empty;

        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string BusinessHours { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumOrderValue { get; set; } = 500m;

        public bool MinimumOrderEnabled { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeDeliveryThreshold { get; set; } = 0m;

        public bool FreeDeliveryEnabled { get; set; } = false;

        public bool CashOnDeliveryEnabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
