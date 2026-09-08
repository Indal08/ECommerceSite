using System.ComponentModel.DataAnnotations;

namespace ECommerceSite.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string Address { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class CheckoutViewModel
    {
        public List<ECommerceSite.Models.CartItem> CartItems { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal DeliveryCharge { get; set; }
        public decimal TotalAmount { get; set; }
        public List<ECommerceSite.Models.DeliveryZone> DeliveryZones { get; set; } = new();

        [Required]
        public int DeliveryZoneId { get; set; }

        [Required, StringLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [Required, Phone, StringLength(20)]
        public string ContactPhone { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Landmark { get; set; } = string.Empty;

        [StringLength(500)]
        public string? DeliveryInstructions { get; set; }
    }
}