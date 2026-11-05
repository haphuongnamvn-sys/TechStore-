using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BrandsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BrandsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>GET /api/brands</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var brands = await _context.Brands
                .AsNoTracking()
                .OrderBy(b => b.Id)
                .Select(b => new { b.Id, b.Name, b.LogoUrl, b.Country })
                .ToListAsync();
            return Ok(brands);
        }
    }
}
