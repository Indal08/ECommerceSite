using ECommerceSite.Data;
using ECommerceSite.Helpers;
using ECommerceSite.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceSite.Services
{
    public class CartService : ICartService
    {
        private const decimal MaxQuantityPerItem = CartQuantityValidator.MaxQuantity;

        private readonly ApplicationDbContext _context;

        public CartService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CartItem>> GetCartItemsAsync(string cartId)
        {
            return await _context.CartItems
                .Include(c => c.Product)
                    .ThenInclude(p => p!.Category)
                .Where(c => c.SessionId == cartId)
                .OrderBy(c => c.Id)
                .ToListAsync();
        }

        public async Task AddToCartAsync(string cartId, int productId, decimal quantity)
        {
            if (!CartQuantityValidator.IsValidQuantity(quantity))
            {
                throw new InvalidOperationException("Quantity must be between 0.25 KG and 50 KG in 0.25 KG increments.");
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsAvailable);

            if (product == null)
            {
                throw new InvalidOperationException("This product is no longer available.");
            }

            var existing = await _context.CartItems
                .FirstOrDefaultAsync(c => c.SessionId == cartId && c.ProductId == productId);

            var nextQty = existing != null ? existing.Quantity + quantity : quantity;
            if (nextQty > MaxQuantityPerItem)
            {
                throw new InvalidOperationException("The total quantity for this product exceeds the allowed limit.");
            }

            if (existing != null)
            {
                existing.Quantity = nextQty;
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    SessionId = cartId,
                    ProductId = productId,
                    Quantity = quantity
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateQuantityAsync(string cartId, int cartItemId, decimal quantity)
        {
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.SessionId == cartId);

            if (item == null)
            {
                throw new InvalidOperationException("Cart item not found.");
            }

            if (quantity <= 0m)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
                return;
            }

            if (!CartQuantityValidator.IsValidQuantity(quantity))
            {
                throw new InvalidOperationException("Quantity must be between 0.25 KG and 50 KG in 0.25 KG increments.");
            }

            item.Quantity = quantity;
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFromCartAsync(string cartId, int cartItemId)
        {
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.SessionId == cartId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearCartAsync(string cartId)
        {
            var items = await _context.CartItems.Where(c => c.SessionId == cartId).ToListAsync();
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> GetCartTotalAsync(string cartId)
        {
            return await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.SessionId == cartId)
                .SumAsync(c => c.Quantity * (c.Product != null ? c.Product.PricePerKg : 0m));
        }
    }
}
