using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceSite.Models
{
    public class CartItem
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }
    }
}
