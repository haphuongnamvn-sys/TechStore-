using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        private static readonly Dictionary<string, List<string>> DefaultSpecTemplatesBySlug = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dien-thoai"] = new() { "Màn hình", "Bộ vi xử lý", "RAM", "Bộ nhớ trong", "Camera", "Dung lượng pin", "Hệ điều hành", "Kết nối" },
            ["laptop"] = new() { "CPU", "RAM", "Ổ cứng", "Card đồ họa", "Kích thước màn hình", "Độ phân giải", "Hệ điều hành", "Dung lượng pin" },
            ["am-thanh"] = new() { "Loại tai nghe", "Kết nối", "Thời lượng pin", "Công nghệ chống ồn", "Chuẩn chống nước", "Micro", "Cổng sạc" },
            ["dong-ho-thong-minh"] = new() { "Kích thước màn hình", "Công nghệ màn hình", "Thời lượng pin", "Cảm biến", "Khả năng chống nước", "Kết nối", "Hệ điều hành tương thích" },
            ["may-tinh-bang"] = new() { "Màn hình", "Bộ vi xử lý", "RAM", "Bộ nhớ trong", "Camera", "Dung lượng pin", "Hệ điều hành", "Kết nối" },
            ["phu-kien"] = new() { "Loại phụ kiện", "Công suất / Dung lượng", "Chuẩn kết nối", "Thiết bị tương thích", "Chất liệu / Kích thước" }
        };

        public static List<string> ParseSpecTemplates(string? json, string? slug)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null && list.Count > 0) return list;
                }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(slug) && DefaultSpecTemplatesBySlug.TryGetValue(slug, out var def))
            {
                return def;
            }
            return new List<string>();
        }

        private static string GenerateSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var clean = sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLower().Replace("đ", "d");
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[^a-z0-9\s-]", "");
            return System.Text.RegularExpressions.Regex.Replace(clean.Trim(), @"\s+", "-");
        }

        /// <summary>GET /api/categories?search=...&includeInactive=true</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search = null, [FromQuery] bool includeInactive = true)
        {
            var query = _context.Categories
                .AsNoTracking()
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            var list = await query.OrderBy(c => c.Id).ToListAsync();

            var productCounts = await _context.Products
                .AsNoTracking()
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                list = list.Where(c =>
                    c.Name.ToLower().Contains(q) ||
                    c.Slug.ToLower().Contains(q) ||
                    (c.Code != null && c.Code.ToLower().Contains(q)) ||
                    (c.Description != null && c.Description.ToLower().Contains(q))
                ).ToList();
            }

            var result = list.Select(c =>
            {
                productCounts.TryGetValue(c.Id, out var count);
                return new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    Code = !string.IsNullOrWhiteSpace(c.Code) ? c.Code : $"CAT-{c.Id:D2}",
                    Description = c.Description ?? string.Empty,
                    ImageUrl = c.ImageUrl ?? string.Empty,
                    ParentId = c.ParentId,
                    IsActive = c.IsActive,
                    ProductCount = count,
                    SpecTemplates = ParseSpecTemplates(c.SpecTemplatesJson, c.Slug)
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var c = await _context.Categories.AsNoTracking().Include(x => x.Products).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { message = "Không tìm thấy danh mục" });

            return Ok(new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Code = !string.IsNullOrWhiteSpace(c.Code) ? c.Code : $"CAT-{c.Id:D2}",
                Description = c.Description ?? string.Empty,
                ImageUrl = c.ImageUrl ?? string.Empty,
                ParentId = c.ParentId,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count,
                SpecTemplates = ParseSpecTemplates(c.SpecTemplatesJson, c.Slug)
            });
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] SaveCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var name = dto.Name.Trim();
            var slug = !string.IsNullOrWhiteSpace(dto.Slug) ? GenerateSlug(dto.Slug) : GenerateSlug(name);
            if (string.IsNullOrWhiteSpace(slug)) slug = "danh-muc-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (await _context.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
                return BadRequest(new { message = "Tên danh mục đã tồn tại trong hệ thống" });

            if (await _context.Categories.AnyAsync(c => c.Slug.ToLower() == slug.ToLower()))
                slug = slug + "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000;

            var specList = dto.SpecTemplates?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? new List<string>();

            var category = new Category
            {
                Name = name,
                Slug = slug,
                Code = string.Empty,
                Description = dto.Description?.Trim() ?? string.Empty,
                ImageUrl = dto.ImageUrl?.Trim() ?? string.Empty,
                IsActive = dto.IsActive,
                SpecTemplatesJson = specList.Count > 0 ? JsonSerializer.Serialize(specList) : string.Empty
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            // Mã danh mục gắn cố định với Primary Key (Id) do hệ thống sinh lúc tạo, không được sửa
            category.Code = $"CAT-{category.Id:D2}";
            await _context.SaveChangesAsync();
            Web.Services.ProductService.InvalidateCache();

            return CreatedAtAction(nameof(GetById), new { id = category.Id }, new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Code = category.Code,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                IsActive = category.IsActive,
                ProductCount = 0,
                SpecTemplates = ParseSpecTemplates(category.SpecTemplatesJson, category.Slug)
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var category = await _context.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return NotFound(new { message = "Không tìm thấy danh mục" });

            var name = dto.Name.Trim();
            if (await _context.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
                return BadRequest(new { message = "Tên danh mục đã tồn tại" });

            var slug = !string.IsNullOrWhiteSpace(dto.Slug) ? GenerateSlug(dto.Slug) : category.Slug;
            if (!string.IsNullOrWhiteSpace(slug) && slug != category.Slug)
            {
                if (await _context.Categories.AnyAsync(c => c.Id != id && c.Slug.ToLower() == slug.ToLower()))
                    return BadRequest(new { message = "Slug danh mục đã tồn tại" });
                category.Slug = slug;
            }

            category.Name = name;
            // KHÔNG cho phép chỉnh sửa Primary Key (Id) và Mã danh mục (Code) do hệ thống sinh lúc tạo
            if (string.IsNullOrWhiteSpace(category.Code))
                category.Code = $"CAT-{category.Id:D2}";
            category.Description = dto.Description?.Trim() ?? string.Empty;
            category.ImageUrl = dto.ImageUrl?.Trim() ?? string.Empty;
            category.IsActive = dto.IsActive;

            if (dto.SpecTemplates != null)
            {
                var specList = dto.SpecTemplates.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
                category.SpecTemplatesJson = JsonSerializer.Serialize(specList);
            }

            await _context.SaveChangesAsync();
            Web.Services.ProductService.InvalidateCache();

            return Ok(new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Code = category.Code,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                IsActive = category.IsActive,
                ProductCount = category.Products.Count,
                SpecTemplates = ParseSpecTemplates(category.SpecTemplatesJson, category.Slug)
            });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return NotFound(new { message = "Không tìm thấy danh mục" });

            if (category.Products.Any())
            {
                return BadRequest(new
                {
                    message = $"Danh mục \"{category.Name}\" đang có {category.Products.Count} sản phẩm liên kết. Không thể xóa để tránh mất dữ liệu sản phẩm. Vui lòng chuyển sản phẩm sang danh mục khác hoặc chuyển trạng thái danh mục sang Tạm ẩn."
                });
            }

            var hasChildren = await _context.Categories.AnyAsync(c => c.ParentId == id);
            if (hasChildren)
            {
                return BadRequest(new { message = "Danh mục này đang có danh mục con liên kết, không thể xóa." });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa danh mục thành công" });
        }
    }
}
