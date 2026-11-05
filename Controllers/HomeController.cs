using Microsoft.AspNetCore.Mvc;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductService _productService;

        public HomeController(IProductService productService)
        {
            _productService = productService;
        }

        public async Task<IActionResult> Index(string? category, string? brand, string? search, string? sort)
        {
            var products = await _productService.GetAllProductsAsync(category, brand, search, sort);
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentBrand = brand;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentSort = sort;
            return View(products);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }
    }
}
