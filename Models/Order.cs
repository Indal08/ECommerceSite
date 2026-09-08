using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace ECommerceSite.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required, StringLength(64)]
        public string AccessToken { get; set; } = GenerateToken();

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Required, StringLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string ContactPhone { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Landmark { get; set; } = string.Empty;

        [StringLength(500)]
        public string? DeliveryInstructions { get; set; }

        [Required, StringLength(100)]
        public string DeliveryZoneName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryCharge { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(30)]
        public string PaymentMethod { get; set; } = "CashOnDelivery";

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public OrderStatus Status { get; set; } = OrderStatus.NewOrder;

        public string? DeliveryBoyName { get; set; }
        public string? DeliveryBoyContact { get; set; }
        public string? RazorpayOrderId { get; set; }
        public string? RazorpayPaymentId { get; set; }
        public bool IsPaid { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
