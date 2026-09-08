using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceSite.Models
{
    public class DeliveryZone
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinDistanceKm { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxDistanceKm { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryCharge { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
