using ECommerceSite.Helpers;
using ECommerceSite.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceSite.Controllers
{
    // No login required - customers order as guests.
    public class CartController : Controller
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        private string CartId => CartCookie.GetOrCreateCartId(HttpContext);
        private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        public async Task<IActionResult> Index()
        {
            var items = await _cartService.GetCartItemsAsync(CartId);
            ViewBag.Total = await _cartService.GetCartTotalAsync(CartId);
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([FromForm] int productId, [FromForm] decimal quantity = 1m)
        {
            if (productId <= 0 || !CartQuantityValidator.IsValidQuantity(quantity))
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Please enter a valid quantity in 0.25 KG increments before adding to the cart." });
                }

                TempData["Error"] = "Please enter a valid quantity in 0.25 KG increments before adding to the cart.";
                return RedirectToAction("Index");
            }

            try
            {
                await _cartService.AddToCartAsync(CartId, productId, quantity);
            }
            catch (InvalidOperationException ex)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = ex.Message });
                }

                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }

            if (IsAjaxRequest())
            {
                var cartItems = await _cartService.GetCartItemsAsync(CartId);
                return Json(new { success = true, itemCount = cartItems.Count });
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, decimal quantity)
        {
            if (cartItemId <= 0 || !CartQuantityValidator.IsValidQuantity(quantity))
            {
                var message = "Please enter a valid quantity in 0.25 KG increments before updating the cart.";
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message });
                }

                TempData["Error"] = message;
                return RedirectToAction("Index");
            }

            await _cartService.UpdateQuantityAsync(CartId, cartItemId, quantity);
            var cartItems = await _cartService.GetCartItemsAsync(CartId);
            var cartItem = cartItems.FirstOrDefault(item => item.Id == cartItemId);
            var subtotal = cartItem != null && cartItem.Product != null ? cartItem.Product.PricePerKg * cartItem.Quantity : 0m;
            var total = await _cartService.GetCartTotalAsync(CartId);

            if (IsAjaxRequest())
            {
                return Json(new { success = true, itemSubtotal = subtotal, total, itemCount = cartItems.Count });
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            await _cartService.RemoveFromCartAsync(CartId, cartItemId);
            var cartItems = await _cartService.GetCartItemsAsync(CartId);
            var total = await _cartService.GetCartTotalAsync(CartId);

            if (IsAjaxRequest())
            {
                return Json(new { success = true, total, itemCount = cartItems.Count, removed = true });
            }

            return RedirectToAction("Index");
        }
    }
}
