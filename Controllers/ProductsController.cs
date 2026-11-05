using Microsoft.AspNetCore.Mvc;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>GET /api/products?category=dien-thoai&brand=Apple&search=iphone&sort=price_asc</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? category,
            [FromQuery] string? brand,
            [FromQuery] string? search,
            [FromQuery] string? sort)
        {
            var products = await _productService.GetAllProductsAsync(category, brand, search, sort);
            return Ok(products);
        }

        /// <summary>GET /api/products/{id}</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound(new { message = "Sản phẩm không tồn tại" });
            return Ok(product);
        }

        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var product = await _productService.GetProductBySlugAsync(slug);
            if (product == null) return NotFound(new { message = "Sản phẩm không tồn tại" });
            return Ok(product);
        }

        /// <summary>POST /api/products</summary>
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] Web.Models.CreateProductDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _productService.CreateProductAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>PUT /api/products/{id}</summary>
        [HttpPut("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Web.Models.UpdateProductDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _productService.UpdateProductAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        /// <summary>DELETE /api/products/{id}</summary>
        [HttpDelete("{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _productService.DeleteProductAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        /// <summary>PATCH /api/products/{id}/stock</summary>
        [HttpPatch("{id}/stock")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin,warehouse")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] Web.Models.UpdateStockDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var success = await _productService.UpdateStockAsync(id, dto.StockQuantity, dto.VariantId);
            if (!success) return NotFound();
            return Ok();
        }
    }
}
