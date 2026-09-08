using ECommerceSite.Models;

namespace ECommerceSite.Services
{
    public interface ICartService
    {
        Task<List<CartItem>> GetCartItemsAsync(string cartId);
        Task AddToCartAsync(string cartId, int productId, decimal quantity);
        Task UpdateQuantityAsync(string cartId, int cartItemId, decimal quantity);
        Task RemoveFromCartAsync(string cartId, int cartItemId);
        Task ClearCartAsync(string cartId);
        Task<decimal> GetCartTotalAsync(string cartId);
    }
}
