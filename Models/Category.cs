using System.ComponentModel.DataAnnotations;

namespace ECommerceSite.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Lets the admin "deactivate" a category instead of hard-deleting it
        // when it still has products attached.
        public bool IsActive { get; set; } = true;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
