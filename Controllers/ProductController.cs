using Microsoft.AspNetCore.Mvc;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly IReviewService _reviewService;

        public ProductController(IProductService productService, IReviewService reviewService)
        {
            _productService = productService;
            _reviewService = reviewService;
        }

        [Route("product/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            ViewBag.Reviews = product.Reviews;

            return View("~/Views/Product/Detail.cshtml", product);
        }
    }
}
