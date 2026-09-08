using ECommerceSite.Data;
using ECommerceSite.Helpers;
using ECommerceSite.Models;
using ECommerceSite.Services;
using ECommerceSite.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceSite.Controllers
{
    public class OrderController : Controller
    {
        private const string MyOrdersCookieName = "MyOrderAccess";

        private readonly ApplicationDbContext _context;
        private readonly ICartService _cartService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public OrderController(
            ApplicationDbContext context,
            ICartService cartService,
            IEmailService emailService,
            IConfiguration config)
        {
            _context = context;
            _cartService = cartService;
            _emailService = emailService;
            _config = config;
        }

        private string CartId => CartCookie.GetOrCreateCartId(HttpContext);

        private List<(int Id, string Token)> GetRememberedOrders()
        {
            if (!Request.Cookies.TryGetValue(MyOrdersCookieName, out var cookieValue) || string.IsNullOrWhiteSpace(cookieValue))
            {
                return new List<(int Id, string Token)>();
            }

            return cookieValue
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Split(':', 2))
                .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out var id) && id > 0 && !string.IsNullOrWhiteSpace(parts[1]))
                .Select(parts => (Id: int.Parse(parts[0]), Token: parts[1]))
                .Distinct()
                .ToList();
        }

        private void RememberOrder(Order order)
        {
            var rememberedOrders = GetRememberedOrders();
            rememberedOrders.RemoveAll(item => item.Id == order.Id);
            rememberedOrders.Insert(0, (order.Id, order.AccessToken));

            Response.Cookies.Append(
                MyOrdersCookieName,
                string.Join('|', rememberedOrders.Select(item => $"{item.Id}:{item.Token}")),
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddDays(90)
                });
        }

        private async Task<(BusinessSettings Settings, List<DeliveryZone> DeliveryZones)> GetCheckoutSettingsAsync()
        {
            var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
            var zones = await _context.DeliveryZones
                .Where(z => z.IsActive)
                .OrderBy(z => z.MinDistanceKm)
                .ToListAsync();

            return (settings, zones);
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cartItems = await _cartService.GetCartItemsAsync(CartId);
            if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

            var (settings, zones) = await GetCheckoutSettingsAsync();
            var subtotal = cartItems.Sum(item => item.Product != null ? item.Product.PricePerKg * item.Quantity : 0m);
            var selectedZone = zones.FirstOrDefault() ?? new DeliveryZone { Id = 0, Name = "Default", DeliveryCharge = 0m };
            var deliveryCharge = settings.FreeDeliveryEnabled && subtotal >= settings.FreeDeliveryThreshold
                ? 0m
                : selectedZone.DeliveryCharge;

            var model = new CheckoutViewModel
            {
                CartItems = cartItems,
                DeliveryZones = zones,
                DeliveryZoneId = selectedZone.Id,
                SubTotal = subtotal,
                DeliveryCharge = deliveryCharge,
                TotalAmount = subtotal + deliveryCharge,
                CustomerName = string.Empty,
                ContactPhone = string.Empty,
                DeliveryAddress = string.Empty,
                Landmark = string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cartItems = await _cartService.GetCartItemsAsync(CartId);
            if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

            var (settings, zones) = await GetCheckoutSettingsAsync();
            model.CartItems = cartItems;
            model.DeliveryZones = zones;

            if (!ModelState.IsValid)
            {
                model.SubTotal = cartItems.Sum(item => item.Product != null ? item.Product.PricePerKg * item.Quantity : 0m);
                var zone = zones.FirstOrDefault(z => z.Id == model.DeliveryZoneId) ?? zones.FirstOrDefault() ?? new DeliveryZone { Id = 0, Name = "Default", DeliveryCharge = 0m };
                model.DeliveryCharge = settings.FreeDeliveryEnabled && model.SubTotal >= settings.FreeDeliveryThreshold ? 0m : zone.DeliveryCharge;
                model.TotalAmount = model.SubTotal + model.DeliveryCharge;
                return View(model);
            }

            foreach (var item in cartItems)
            {
                if (item.Product == null)
                {
                    ModelState.AddModelError(string.Empty, "One or more products were removed and need to be refreshed.");
                    return View(model);
                }

                if (!item.Product.IsAvailable)
                {
                    ModelState.AddModelError(string.Empty, $"{item.Product.Name} is no longer available. Please remove it from your cart.");
                    return View(model);
                }

                if (!CartQuantityValidator.IsValidQuantity(item.Quantity))
                {
                    ModelState.AddModelError(string.Empty, "Each item quantity must be a valid value in 0.25 KG increments, up to 50 KG.");
                    return View(model);
                }
            }

            var selectedZone = zones.FirstOrDefault(z => z.Id == model.DeliveryZoneId && z.IsActive);
            if (selectedZone == null)
            {
                ModelState.AddModelError(nameof(model.DeliveryZoneId), "Please select a valid delivery zone.");
                return View(model);
            }

            decimal subtotal = 0m;
            var orderItems = new List<OrderItem>();

            foreach (var cartItem in cartItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == cartItem.ProductId && p.IsAvailable);
                if (product == null)
                {
                    ModelState.AddModelError(string.Empty, "A product became unavailable before checkout. Please refresh the cart and try again.");
                    return View(model);
                }

                if (!CartQuantityValidator.IsValidQuantity(cartItem.Quantity))
                {
                    ModelState.AddModelError(string.Empty, "One or more cart quantities are invalid. Please update your cart and try again.");
                    return View(model);
                }

                var unitPrice = product.PricePerKg;
                var totalPrice = Math.Round(unitPrice * cartItem.Quantity, 2, MidpointRounding.AwayFromZero);
                subtotal += totalPrice;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductNameSnapshot = product.Name,
                    Quantity = cartItem.Quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = totalPrice
                });
            }

            if (settings.MinimumOrderEnabled && subtotal < settings.MinimumOrderValue)
            {
                ModelState.AddModelError(string.Empty, $"Minimum order value is Rs. {settings.MinimumOrderValue:N0}. Please add more items to continue.");
                model.SubTotal = subtotal;
                model.DeliveryCharge = settings.FreeDeliveryEnabled && subtotal >= settings.FreeDeliveryThreshold ? 0m : selectedZone.DeliveryCharge;
                model.TotalAmount = subtotal + model.DeliveryCharge;
                return View(model);
            }

            var deliveryCharge = settings.FreeDeliveryEnabled && subtotal >= settings.FreeDeliveryThreshold ? 0m : selectedZone.DeliveryCharge;
            var totalAmount = subtotal + deliveryCharge;

            var order = new Order
            {
                OrderNumber = string.Empty,
                AccessToken = Guid.NewGuid().ToString("N"),
                CustomerName = model.CustomerName.Trim(),
                ContactPhone = model.ContactPhone.Trim(),
                DeliveryAddress = model.DeliveryAddress.Trim(),
                Landmark = model.Landmark.Trim(),
                DeliveryInstructions = string.IsNullOrWhiteSpace(model.DeliveryInstructions) ? null : model.DeliveryInstructions.Trim(),
                DeliveryZoneName = selectedZone.Name,
                SubTotal = subtotal,
                DeliveryCharge = deliveryCharge,
                TotalAmount = totalAmount,
                PaymentMethod = "CashOnDelivery",
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.NewOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = orderItems
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            order.OrderNumber = $"ORD{order.Id:000000}";
            await _context.SaveChangesAsync();

            RememberOrder(order);

            await _cartService.ClearCartAsync(CartId);

            var adminEmail = _config["Smtp:AdminEmail"];
            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                var body = $"New order {order.OrderNumber} received. Total: Rs.{order.TotalAmount:N2}. Customer: {order.CustomerName} ({order.ContactPhone}). Delivery: {order.DeliveryAddress}, near {order.Landmark}. Payment: Cash on Delivery.";
                try
                {
                    await _emailService.SendAsync(adminEmail, $"New Order {order.OrderNumber}", body);
                }
                catch (Exception)
                {
                    // Keep checkout reliable even if SMTP is not configured yet.
                }
            }

            return RedirectToAction(nameof(OrderSuccess), new { id = order.Id, token = order.AccessToken });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeliverySummary(int deliveryZoneId)
        {
            var cartItems = await _cartService.GetCartItemsAsync(CartId);
            if (!cartItems.Any())
            {
                return Json(new { success = false, message = "Your cart is empty." });
            }

            var (settings, zones) = await GetCheckoutSettingsAsync();
            var subtotal = cartItems.Sum(item => item.Product != null ? item.Product.PricePerKg * item.Quantity : 0m);
            var selectedZone = zones.FirstOrDefault(z => z.Id == deliveryZoneId && z.IsActive)
                ?? zones.FirstOrDefault()
                ?? new DeliveryZone { Id = 0, Name = "Default", DeliveryCharge = 0m };

            var deliveryCharge = settings.FreeDeliveryEnabled && subtotal >= settings.FreeDeliveryThreshold
                ? 0m
                : selectedZone.DeliveryCharge;

            return Json(new
            {
                success = true,
                subtotal,
                deliveryCharge,
                totalAmount = subtotal + deliveryCharge
            });
        }

        [HttpGet]
        public async Task<IActionResult> OrderSuccess(int id, string token)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(token))
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.AccessToken == token);

            if (order == null) return NotFound();

            var businessSettings = await _context.BusinessSettings.FirstOrDefaultAsync();
            ViewBag.ShopWhatsApp = string.IsNullOrWhiteSpace(businessSettings?.WhatsApp)
                ? _config["Business:WhatsApp"]
                : businessSettings.WhatsApp;
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            var rememberedOrders = GetRememberedOrders();
            if (rememberedOrders.Count == 0)
            {
                return View(new List<Order>());
            }

            var orderIds = rememberedOrders.Select(item => item.Id).ToList();
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => orderIds.Contains(o.Id))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var validOrders = orders
                .Where(order => rememberedOrders.Any(item => item.Id == order.Id && item.Token == order.AccessToken))
                .ToList();

            return View(validOrders);
        }

        [HttpGet]
        public async Task<IActionResult> MyOrdersStatus()
        {
            var rememberedOrders = GetRememberedOrders();
            if (rememberedOrders.Count == 0)
            {
                return Json(new { orders = Array.Empty<object>() });
            }

            var activeOrders = rememberedOrders
                .Where(item => item.Id > 0)
                .Select(item => item.Id)
                .ToList();

            var orders = await _context.Orders
                .Where(order => activeOrders.Contains(order.Id))
                .Select(order => new { order.Id, order.OrderNumber, order.Status, order.AccessToken })
                .ToListAsync();

            var verifiedOrders = orders
                .Where(order => rememberedOrders.Any(item => item.Id == order.Id && item.Token == order.AccessToken))
                .Select(order => new
                {
                    id = order.Id,
                    orderNumber = order.OrderNumber,
                    status = order.Status.ToString(),
                    isTerminal = OrderStatusValidator.IsTerminalStatus(order.Status)
                })
                .ToList();

            return Json(new { orders = verifiedOrders });
        }

    }
}
