using ECommerceSite.Data;
using ECommerceSite.Models;
using ECommerceSite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceSite.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;

            var model = new AdminDashboardViewModel
            {
                TodayOrdersCount = await _context.Orders.CountAsync(o => o.CreatedAt.Date == today),
                NewOrdersCount = await _context.Orders.CountAsync(o => o.Status == OrderStatus.NewOrder),
                AcceptedOrdersCount = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Accepted),
                DeliveredOrdersCount = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Delivered),
                CancelledOrdersCount = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled),
                TodaySales = await _context.Orders
                    .Where(o => o.CreatedAt.Date == today && o.Status != OrderStatus.Cancelled)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m,
                RecentOrders = await _context.Orders
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .ToListAsync()
            };

            return View(model);
        }

        // ---------- Products ----------

        public async Task<IActionResult> Products()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> ProductCreate()
        {
            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductCreate(Product product)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
                return View(product);
            }

            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Products));
        }

        [HttpGet]
        public async Task<IActionResult> ProductEdit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductEdit(Product product)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
                return View(product);
            }

            var existing = await _context.Products.FindAsync(product.Id);
            if (existing == null) return NotFound();

            existing.Name = product.Name;
            existing.Description = product.Description;
            existing.PricePerKg = product.PricePerKg;
            existing.ImageUrl = product.ImageUrl;
            existing.IsAvailable = product.IsAvailable;
            existing.CategoryId = product.CategoryId;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                var usedInOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
                if (usedInOrders)
                {
                    // Keep order history intact - just hide it from customers.
                    product.IsAvailable = false;
                    product.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.Products.Remove(product);
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleProductAvailability(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsAvailable = !product.IsAvailable;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                return Json(new { success = true, isAvailable = product.IsAvailable, label = product.IsAvailable ? "Yes" : "No" });
            }

            return RedirectToAction(nameof(Products));
        }

        // ---------- Categories ----------

        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories.ToListAsync();
            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryCreate(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.Categories.Add(new Category { Name = name, IsActive = true });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryDelete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
                if (hasProducts)
                {
                    // Can't hard-delete without breaking product history - deactivate instead.
                    category.IsActive = false;
                }
                else
                {
                    _context.Categories.Remove(category);
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryActivate(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                category.IsActive = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Categories));
        }

        // ---------- Orders ----------

        public async Task<IActionResult> Orders(OrderStatus? status, string? searchTerm)
        {
            var query = _context.Orders.AsQueryable();

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(o =>
                    o.OrderNumber.Contains(term) ||
                    o.CustomerName.Contains(term) ||
                    o.ContactPhone.Contains(term) ||
                    o.DeliveryAddress.Contains(term) ||
                    o.Landmark.Contains(term));
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.TotalOrders = orders.Count;
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> NewOrderCount()
        {
            var latestOrder = await _context.Orders
                .Where(o => o.Status == OrderStatus.NewOrder)
                .OrderByDescending(o => o.Id)
                .Select(o => new { o.Id })
                .FirstOrDefaultAsync();

            var count = await _context.Orders.CountAsync(o => o.Status == OrderStatus.NewOrder);
            return Json(new
            {
                count,
                latestOrderId = latestOrder?.Id ?? 0
            });
        }

        [HttpGet]
        public async Task<IActionResult> DashboardCounts()
        {
            return Json(new
            {
                newOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.NewOrder),
                accepted = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Accepted),
                delivered = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Delivered)
            });
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
            var zones = await _context.DeliveryZones.OrderBy(z => z.MinDistanceKm).ToListAsync();
            ViewBag.DeliveryZones = zones;
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(BusinessSettings model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.DeliveryZones = await _context.DeliveryZones.OrderBy(z => z.MinDistanceKm).ToListAsync();
                return View(model);
            }

            var settings = await _context.BusinessSettings.FirstOrDefaultAsync() ?? new BusinessSettings();
            settings.BusinessName = model.BusinessName;
            settings.Phone = model.Phone;
            settings.WhatsApp = model.WhatsApp;
            settings.ServiceArea = model.ServiceArea;
            settings.Address = model.Address;
            settings.BusinessHours = model.BusinessHours;
            settings.MinimumOrderValue = model.MinimumOrderValue;
            settings.MinimumOrderEnabled = model.MinimumOrderEnabled;
            settings.FreeDeliveryThreshold = model.FreeDeliveryThreshold;
            settings.FreeDeliveryEnabled = model.FreeDeliveryEnabled;
            settings.CashOnDeliveryEnabled = model.CashOnDeliveryEnabled;
            settings.UpdatedAt = DateTime.UtcNow;

            if (settings.Id == 0)
            {
                _context.BusinessSettings.Add(settings);
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "Business settings saved successfully.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpGet]
        public async Task<IActionResult> DeliveryZones()
        {
            var zones = await _context.DeliveryZones.OrderBy(z => z.MinDistanceKm).ToListAsync();
            return View(zones);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeliveryZoneCreate(DeliveryZone model)
        {
            if (!ModelState.IsValid || model.MinDistanceKm >= model.MaxDistanceKm || model.DeliveryCharge < 0)
            {
                TempData["Message"] = "Please provide valid delivery zone details.";
                return RedirectToAction(nameof(DeliveryZones));
            }

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            _context.DeliveryZones.Add(model);
            await _context.SaveChangesAsync();
            TempData["Message"] = "Delivery zone added.";
            return RedirectToAction(nameof(DeliveryZones));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeliveryZoneDelete(int id)
        {
            var zone = await _context.DeliveryZones.FindAsync(id);
            if (zone != null)
            {
                _context.DeliveryZones.Remove(zone);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(DeliveryZones));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (!OrderStatusValidator.IsValidTransition(order.Status, OrderStatus.Accepted))
            {
                var message = "This order cannot be accepted.";
                if (IsAjaxRequest()) return Json(new { success = false, message });
                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            order.Status = OrderStatus.Accepted;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (IsAjaxRequest()) return Json(new { success = true, status = order.Status.ToString() });
            TempData["Message"] = "Order accepted.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (!OrderStatusValidator.IsValidTransition(order.Status, OrderStatus.Cancelled))
            {
                var message = "This order cannot be rejected.";
                if (IsAjaxRequest()) return Json(new { success = false, message });
                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (IsAjaxRequest()) return Json(new { success = true, status = order.Status.ToString() });
            TempData["Message"] = "Order cancelled.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkOrderDelivered(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.Delivered && order.PaymentStatus == PaymentStatus.Paid)
            {
                var message = "This order has already been delivered and paid.";
                if (IsAjaxRequest()) return Json(new { success = true, status = order.Status.ToString(), paymentStatus = order.PaymentStatus.ToString(), message });
                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            if (!OrderStatusValidator.IsValidTransition(order.Status, OrderStatus.Delivered))
            {
                var message = "This order cannot be marked delivered.";
                if (IsAjaxRequest()) return Json(new { success = false, message });
                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            order.Status = OrderStatus.Delivered;
            order.PaymentStatus = PaymentStatus.Paid;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (IsAjaxRequest()) return Json(new { success = true, status = order.Status.ToString(), paymentStatus = order.PaymentStatus.ToString() });
            TempData["Message"] = "Order marked as delivered.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int id, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (!OrderStatusValidator.IsValidTransition(order.Status, status))
            {
                var message = "That status change is not allowed for this order.";
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message });
                }

                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;

            if (status == OrderStatus.Delivered)
            {
                order.PaymentStatus = PaymentStatus.Paid;
            }

            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                return Json(new { success = true, status = order.Status.ToString(), paymentStatus = order.PaymentStatus.ToString() });
            }

            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaymentReceived(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (!OrderStatusValidator.CanProcessPayment(order.Status))
            {
                var message = "Cannot mark payment for a cancelled order.";
                if (IsAjaxRequest()) return Json(new { success = false, message });
                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            if (OrderStatusValidator.IsFullyCompleted(order.Status, order.PaymentStatus))
            {
                var message = "Payment already received and order already delivered.";
                if (IsAjaxRequest()) return Json(new { success = true, status = order.Status.ToString(), paymentStatus = order.PaymentStatus.ToString(), message });
                TempData["Message"] = message;
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            order.PaymentStatus = PaymentStatus.Paid;
            order.UpdatedAt = DateTime.UtcNow;
            order.Status = OrderStatusValidator.ApplyPaymentReceived(order.Status);

            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    status = order.Status.ToString(),
                    paymentStatus = order.PaymentStatus.ToString()
                });
            }

            return RedirectToAction(nameof(OrderDetails), new { id });
        }
    }
}
