using ECommerceSite.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceSite.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? categoryId, bool partial = false)
        {
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable && p.Category != null && p.Category.IsActive)
                .AsQueryable();

            if (categoryId.HasValue)
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);

            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            ViewBag.SelectedCategoryId = categoryId;

            var products = await productsQuery.ToListAsync();
            if (partial)
            {
                return PartialView("_ProductGrid", products);
            }

            return View(products);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
