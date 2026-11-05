using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private static List<ProductListDto>? _cachedProducts;
        private static readonly SemaphoreSlim _cacheLock = new(1, 1);

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public static void InvalidateCache()
        {
            _cachedProducts = null;
        }

        private async Task<List<ProductListDto>> GetOrLoadCachedProductsAsync()
        {
            var cached = _cachedProducts;
            if (cached != null) return cached;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedProducts != null) return _cachedProducts;

                var products = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Brand)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                var images = await _context.ProductImages
                    .AsNoTracking()
                    .OrderBy(i => i.SortOrder)
                    .ToListAsync();

                var specs = await _context.ProductSpecs
                    .AsNoTracking()
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();

                var highlights = await _context.ProductHighlights
                    .AsNoTracking()
                    .OrderBy(h => h.SortOrder)
                    .ToListAsync();

                var variants = await _context.ProductVariants
                    .AsNoTracking()
                    .ToListAsync();

                var reviewCounts = await _context.Reviews
                    .AsNoTracking()
                    .GroupBy(r => r.ProductId)
                    .Select(g => new { ProductId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProductId, x => x.Count);

                var imgLookup = images.ToLookup(x => x.ProductId);
                var specLookup = specs.ToLookup(x => x.ProductId);
                var hlLookup = highlights.ToLookup(x => x.ProductId);
                var varLookup = variants.ToLookup(x => x.ProductId);

                var list = new List<ProductListDto>(products.Count);
                foreach (var p in products)
                {
                    var pImgs = imgLookup[p.Id].ToList();
                    var pSpecs = specLookup[p.Id].ToList();
                    var pHls = hlLookup[p.Id].ToList();
                    var pVars = varLookup[p.Id].ToList();
                    reviewCounts.TryGetValue(p.Id, out var revCount);

                    list.Add(new ProductListDto
                    {
                        Id            = p.Id,
                        ProductCode   = !string.IsNullOrWhiteSpace(p.ProductCode) ? p.ProductCode : $"SP-{p.Id:D3}",
                        Name          = p.Name,
                        Slug          = p.Slug,
                        Price         = p.Price,
                        SalePrice     = p.SalePrice,
                        Badge         = p.Badge,
                        IsActive      = p.IsActive,
                        RatingAvg     = p.RatingAvg,
                        ReviewCount   = revCount,
                        StockQuantity = pVars.Count > 0 ? pVars.Sum(v => v.StockQuantity) : p.StockQuantity,
                        PrimaryImage  = pImgs.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                                        ?? pImgs.FirstOrDefault()?.ImageUrl
                                        ?? pVars.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.ImageUrl))?.ImageUrl ?? "",
                        CategoryName  = p.Category?.Name ?? "",
                        CategorySlug  = p.Category?.Slug ?? "",
                        BrandName     = p.Brand?.Name ?? "",
                        CategoryId    = p.CategoryId,
                        BrandId       = p.BrandId,
                        Description   = p.Description ?? "",
                        Images        = pImgs.Select(i => i.ImageUrl).ToList(),
                        Highlights    = pHls.Select(h => h.Content).ToList(),
                        Specs         = pSpecs.Select(s => new ProductSpecDto { Key = s.SpecKey, Value = s.SpecValue }).ToList(),
                        Variants      = pVars.Select(v => new ProductVariantDto
                        {
                            Id            = v.Id,
                            ProductId     = v.ProductId,
                            VariantName   = v.VariantName,
                            ColorName     = v.ColorName,
                            ColorCode     = v.ColorCode,
                            Price         = v.Price,
                            OriginalPrice = v.OriginalPrice,
                            StockQuantity = v.StockQuantity,
                            Sku           = v.Sku,
                            ImageUrl      = v.ImageUrl
                        }).ToList()
                    });
                }

                _cachedProducts = list;
                return list;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<List<ProductListDto>> GetAllProductsAsync(
            string? category = null, string? brand = null,
            string? search = null, string? sort = null)
        {
            var all = await GetOrLoadCachedProductsAsync();
            IEnumerable<ProductListDto> query = all;

            if (!string.IsNullOrWhiteSpace(category))
            {
                var catLower = category.Trim().ToLower();
                query = query.Where(p =>
                    p.CategorySlug.Equals(catLower, StringComparison.OrdinalIgnoreCase) ||
                    p.CategoryName.Equals(catLower, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var brandLower = brand.Trim().ToLower();
                query = query.Where(p => p.BrandName.Equals(brandLower, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = NormalizeSearchString(search);
                query = query.Where(p =>
                    NormalizeSearchString(p.Name).Contains(s) ||
                    NormalizeSearchString(p.ProductCode).Contains(s) ||
                    NormalizeSearchString(p.BrandName).Contains(s) ||
                    NormalizeSearchString(p.CategoryName).Contains(s) ||
                    (!string.IsNullOrEmpty(p.Description) && NormalizeSearchString(p.Description).Contains(s))
                );
            }

            query = sort switch
            {
                "price_asc"  => query.OrderBy(p => p.SalePrice > 0 ? p.SalePrice : p.Price),
                "price_desc" => query.OrderByDescending(p => p.SalePrice > 0 ? p.SalePrice : p.Price),
                "rating"     => query.OrderByDescending(p => p.RatingAvg),
                _            => query
            };

            return query.ToList();
        }

        private static string NormalizeSearchString(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var result = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    result.Append(c);
                }
            }
            return result.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLower().Replace("đ", "d");
        }

        public async Task<ProductDetailDto?> GetProductByIdAsync(int id)
        {
            var all = await GetOrLoadCachedProductsAsync();
            var item = all.FirstOrDefault(p => p.Id == id);
            if (item == null) return null;
            return await BuildDetailDtoAsync(item);
        }

        public async Task<ProductDetailDto?> GetProductBySlugAsync(string slug)
        {
            var all = await GetOrLoadCachedProductsAsync();
            var item = all.FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (item == null) return null;
            return await BuildDetailDtoAsync(item);
        }

        private async Task<ProductDetailDto> BuildDetailDtoAsync(ProductListDto p)
        {
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.User)
                .Where(r => r.ProductId == p.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id        = r.Id,
                    ProductId = r.ProductId,
                    OrderId   = r.OrderId,
                    Username  = r.User != null ? (!string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.FullName : r.User.Username) : "Ẩn danh",
                    Rating    = r.Rating,
                    Comment   = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return new ProductDetailDto
            {
                Id            = p.Id,
                ProductCode   = p.ProductCode,
                Name          = p.Name,
                Slug          = p.Slug,
                Price         = p.Price,
                SalePrice     = p.SalePrice,
                Badge         = p.Badge,
                IsActive      = p.IsActive,
                RatingAvg     = p.RatingAvg,
                ReviewCount   = reviews.Count,
                StockQuantity = p.StockQuantity,
                PrimaryImage  = p.PrimaryImage,
                CategoryName  = p.CategoryName,
                CategorySlug  = p.CategorySlug,
                BrandName     = p.BrandName,
                CategoryId    = p.CategoryId,
                BrandId       = p.BrandId,
                Description   = p.Description,
                Images        = p.Images,
                Highlights    = p.Highlights,
                Specs         = p.Specs,
                Variants      = p.Variants,
                Reviews       = reviews
            };
        }

        public async Task<ProductListDto> CreateProductAsync(CreateProductDto dto)
        {
            var slugBase = NormalizeSearchString(dto.Name);
            slugBase = System.Text.RegularExpressions.Regex.Replace(slugBase, @"[^a-z0-9\s-]", "");
            var slug = System.Text.RegularExpressions.Regex.Replace(slugBase.Trim(), @"\s+", "-") + "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var effectivePrice = dto.SalePrice > 0 ? dto.SalePrice : dto.Price;

            var product = new Product
            {
                ProductCode = !string.IsNullOrWhiteSpace(dto.ProductCode)
                    ? dto.ProductCode.Trim()
                    : $"SP-{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000:D5}",
                Name = dto.Name.Trim(),
                Slug = slug,
                Price = dto.Price,
                SalePrice = dto.SalePrice,
                Description = dto.Description?.Trim() ?? string.Empty,
                Badge = dto.Badge?.Trim() ?? string.Empty,
                IsActive = dto.IsActive,
                StockQuantity = Math.Max(0, dto.StockQuantity),
                CategoryId = dto.CategoryId,
                BrandId = dto.BrandId,
                CreatedAt = DateTime.UtcNow,
                Images = new List<ProductImage>(),
                Variants = new List<ProductVariant>(),
                Specs = new List<ProductSpec>(),
                Highlights = new List<ProductHighlight>()
            };

            if (dto.Variants != null && dto.Variants.Any())
            {
                int idx = 1;
                foreach (var v in dto.Variants)
                {
                    var vName = string.IsNullOrWhiteSpace(v.VariantName) ? "Tiêu chuẩn" : v.VariantName.Trim();
                    var cName = string.IsNullOrWhiteSpace(v.ColorName) ? "Mặc định" : v.ColorName.Trim();
                    product.Variants.Add(new ProductVariant
                    {
                        VariantName = vName,
                        ColorName = cName,
                        ColorCode = string.IsNullOrWhiteSpace(v.ColorCode) ? "#334155" : v.ColorCode.Trim(),
                        Price = v.Price > 0 ? v.Price : effectivePrice,
                        OriginalPrice = v.OriginalPrice.HasValue && v.OriginalPrice.Value > 0 ? v.OriginalPrice.Value : dto.Price,
                        StockQuantity = Math.Max(0, v.StockQuantity),
                        ImageUrl = !string.IsNullOrWhiteSpace(v.ImageUrl) ? v.ImageUrl.Trim() : dto.PrimaryImage,
                        Sku = !string.IsNullOrWhiteSpace(v.Sku) ? v.Sku.Trim() : $"V3-NEW-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{idx++}"
                    });
                }
                product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);
            }
            else
            {
                product.Variants.Add(new ProductVariant
                {
                    VariantName = "Tiêu chuẩn",
                    ColorName = "Mặc định",
                    ColorCode = "#334155",
                    Price = effectivePrice,
                    OriginalPrice = dto.Price,
                    StockQuantity = Math.Max(0, dto.StockQuantity),
                    ImageUrl = dto.PrimaryImage,
                    Sku = $"V3-NEW-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-1"
                });
            }

            if (dto.Specs != null && dto.Specs.Any())
            {
                int sIdx = 0;
                foreach (var s in dto.Specs.Where(s => !string.IsNullOrWhiteSpace(s.Key) && !string.IsNullOrWhiteSpace(s.Value)))
                {
                    product.Specs.Add(new ProductSpec
                    {
                        SpecKey = s.Key.Trim(),
                        SpecValue = s.Value.Trim(),
                        SortOrder = sIdx++
                    });
                }
            }

            if (dto.Highlights != null && dto.Highlights.Any())
            {
                int hIdx = 0;
                foreach (var h in dto.Highlights.Where(h => !string.IsNullOrWhiteSpace(h)))
                {
                    product.Highlights.Add(new ProductHighlight
                    {
                        Content = h.Trim(),
                        SortOrder = hIdx++
                    });
                }
            }

            SyncProductImages(product, dto.PrimaryImage, dto.Images);

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Mã sản phẩm và SKU biến thể được hệ thống tự động sinh cố định theo Primary Key (Id) lúc tạo
            product.ProductCode = $"SP-{product.Id:D3}";
            int vIdx = 1;
            foreach (var v in product.Variants)
            {
                v.Sku = $"V3-{product.Id:D3}-{vIdx++:D2}";
            }
            await _context.SaveChangesAsync();

            await _context.Entry(product).Reference(p => p.Category).LoadAsync();
            await _context.Entry(product).Reference(p => p.Brand).LoadAsync();

            InvalidateCache();
            return MapToListDto(product);
        }

        public async Task<ProductDetailDto?> UpdateProductAsync(int id, UpdateProductDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Specs)
                .Include(p => p.Highlights)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return null;

            var oldEffectivePrice = product.SalePrice > 0 ? product.SalePrice : product.Price;
            var newEffectivePrice = dto.SalePrice > 0 ? dto.SalePrice : dto.Price;
            var priceDelta = newEffectivePrice - oldEffectivePrice;
            var origPriceDelta = dto.Price - product.Price;

            // KHÔNG cho phép sửa Primary Key (Id) và Mã sản phẩm (ProductCode) do hệ thống sinh ra lúc tạo
            if (string.IsNullOrWhiteSpace(product.ProductCode))
                product.ProductCode = $"SP-{product.Id:D3}";

            product.Name = dto.Name.Trim();
            product.Price = dto.Price;
            product.SalePrice = dto.SalePrice;
            product.Description = dto.Description?.Trim() ?? string.Empty;
            product.Badge = dto.Badge?.Trim() ?? string.Empty;
            product.IsActive = dto.IsActive;
            product.CategoryId = dto.CategoryId;
            product.BrandId = dto.BrandId;

            if (dto.Variants != null && dto.Variants.Any())
            {
                var existingVariants = product.Variants.ToList();
                var keptVariantIds = new HashSet<int>();
                int nextIdx = existingVariants.Count + 1;

                foreach (var v in dto.Variants)
                {
                    var vName = string.IsNullOrWhiteSpace(v.VariantName) ? "Tiêu chuẩn" : v.VariantName.Trim();
                    var cName = string.IsNullOrWhiteSpace(v.ColorName) ? "Mặc định" : v.ColorName.Trim();

                    // Giữ nguyên bản ghi ProductVariant cũ (bảo toàn Primary Key Id và SKU hệ thống) nếu khớp Id hoặc (VariantName + ColorName)
                    var existing = (v.Id > 0 ? existingVariants.FirstOrDefault(ev => ev.Id == v.Id && !keptVariantIds.Contains(ev.Id)) : null)
                                   ?? existingVariants.FirstOrDefault(ev =>
                                       !keptVariantIds.Contains(ev.Id) &&
                                       ev.VariantName.Equals(vName, StringComparison.OrdinalIgnoreCase) &&
                                       ev.ColorName.Equals(cName, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        keptVariantIds.Add(existing.Id);
                        existing.VariantName = vName;
                        existing.ColorName = cName;
                        existing.ColorCode = string.IsNullOrWhiteSpace(v.ColorCode) ? "#334155" : v.ColorCode.Trim();
                        existing.Price = v.Price > 0 ? v.Price : newEffectivePrice;
                        existing.OriginalPrice = v.OriginalPrice.HasValue && v.OriginalPrice.Value > 0 ? v.OriginalPrice.Value : dto.Price;
                        existing.StockQuantity = Math.Max(0, v.StockQuantity);
                        existing.ImageUrl = !string.IsNullOrWhiteSpace(v.ImageUrl) ? v.ImageUrl.Trim() : dto.PrimaryImage;
                        if (string.IsNullOrWhiteSpace(existing.Sku))
                            existing.Sku = $"V3-{product.Id:D3}-{existing.Id:D2}";
                    }
                    else
                    {
                        product.Variants.Add(new ProductVariant
                        {
                            ProductId = product.Id,
                            VariantName = vName,
                            ColorName = cName,
                            ColorCode = string.IsNullOrWhiteSpace(v.ColorCode) ? "#334155" : v.ColorCode.Trim(),
                            Price = v.Price > 0 ? v.Price : newEffectivePrice,
                            OriginalPrice = v.OriginalPrice.HasValue && v.OriginalPrice.Value > 0 ? v.OriginalPrice.Value : dto.Price,
                            StockQuantity = Math.Max(0, v.StockQuantity),
                            ImageUrl = !string.IsNullOrWhiteSpace(v.ImageUrl) ? v.ImageUrl.Trim() : dto.PrimaryImage,
                            Sku = $"V3-{product.Id:D3}-{nextIdx++:D2}"
                        });
                    }
                }

                // Chỉ xóa các biến thể đã bị người dùng gỡ bỏ khỏi danh sách
                var removedVariants = existingVariants.Where(ev => !keptVariantIds.Contains(ev.Id)).ToList();
                if (removedVariants.Any())
                {
                    _context.ProductVariants.RemoveRange(removedVariants);
                    foreach (var rv in removedVariants)
                        product.Variants.Remove(rv);
                }

                product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);
            }
            else if (product.Variants.Any())
            {
                foreach (var v in product.Variants)
                {
                    if (priceDelta != 0)
                        v.Price = Math.Max(0, v.Price + priceDelta);
                    if (origPriceDelta != 0 && v.OriginalPrice.HasValue)
                        v.OriginalPrice = Math.Max(v.Price, v.OriginalPrice.Value + origPriceDelta);
                }

                var currentVariantSum = product.Variants.Sum(v => v.StockQuantity);
                if (dto.StockQuantity != currentVariantSum)
                {
                    var diff = dto.StockQuantity - currentVariantSum;
                    var firstVariant = product.Variants.First();
                    firstVariant.StockQuantity = Math.Max(0, firstVariant.StockQuantity + diff);
                }
                product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);
            }
            else
            {
                product.StockQuantity = Math.Max(0, dto.StockQuantity);
                product.Variants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    VariantName = "Tiêu chuẩn",
                    ColorName = "Mặc định",
                    ColorCode = "#334155",
                    Price = newEffectivePrice,
                    OriginalPrice = dto.Price,
                    StockQuantity = product.StockQuantity,
                    ImageUrl = dto.PrimaryImage,
                    Sku = $"V3-{product.Id:D3}-01"
                });
            }

            if (dto.Specs != null)
            {
                _context.ProductSpecs.RemoveRange(product.Specs);
                product.Specs.Clear();
                int sIdx = 0;
                foreach (var s in dto.Specs.Where(s => !string.IsNullOrWhiteSpace(s.Key) && !string.IsNullOrWhiteSpace(s.Value)))
                {
                    product.Specs.Add(new ProductSpec
                    {
                        ProductId = product.Id,
                        SpecKey = s.Key.Trim(),
                        SpecValue = s.Value.Trim(),
                        SortOrder = sIdx++
                    });
                }
            }

            if (dto.Highlights != null)
            {
                _context.ProductHighlights.RemoveRange(product.Highlights);
                product.Highlights.Clear();
                int hIdx = 0;
                foreach (var h in dto.Highlights.Where(h => !string.IsNullOrWhiteSpace(h)))
                {
                    product.Highlights.Add(new ProductHighlight
                    {
                        ProductId = product.Id,
                        Content = h.Trim(),
                        SortOrder = hIdx++
                    });
                }
            }

            SyncProductImages(product, dto.PrimaryImage, dto.Images);

            await _context.SaveChangesAsync();
            _context.Entry(product).State = EntityState.Detached;
            InvalidateCache();
            return await GetProductByIdAsync(id);
        }

        private void SyncProductImages(Product product, string? primaryImageUrl, List<string>? galleryImages)
        {
            var orderedUrls = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryImageUrl))
            {
                orderedUrls.Add(primaryImageUrl.Trim());
            }

            if (galleryImages != null)
            {
                foreach (var gUrl in galleryImages.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    var url = gUrl.Trim();
                    if (!orderedUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
                    {
                        orderedUrls.Add(url);
                    }
                }
            }

            foreach (var v in product.Variants)
            {
                if (!string.IsNullOrWhiteSpace(v.ImageUrl))
                {
                    var url = v.ImageUrl.Trim();
                    if (!orderedUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
                    {
                        orderedUrls.Add(url);
                    }
                }
            }

            if (galleryImages == null)
            {
                foreach (var existingImg in product.Images.OrderBy(i => i.SortOrder))
                {
                    if (!string.IsNullOrWhiteSpace(existingImg.ImageUrl) &&
                        !orderedUrls.Contains(existingImg.ImageUrl.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        orderedUrls.Add(existingImg.ImageUrl.Trim());
                    }
                }
            }

            _context.ProductImages.RemoveRange(product.Images);
            product.Images.Clear();

            for (int i = 0; i < orderedUrls.Count; i++)
            {
                product.Images.Add(new ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = orderedUrls[i],
                    IsPrimary = i == 0,
                    SortOrder = i
                });
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Specs)
                .Include(p => p.Highlights)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return false;

            var cartItems = await _context.Carts.Where(c => c.ProductId == id).ToListAsync();
            if (cartItems.Any()) _context.Carts.RemoveRange(cartItems);

            var orderItems = await _context.OrderItems.Where(oi => oi.ProductId == id).ToListAsync();
            if (orderItems.Any()) _context.OrderItems.RemoveRange(orderItems);

            if (product.Reviews.Any()) _context.Reviews.RemoveRange(product.Reviews);
            if (product.Specs.Any()) _context.ProductSpecs.RemoveRange(product.Specs);
            if (product.Highlights.Any()) _context.ProductHighlights.RemoveRange(product.Highlights);
            if (product.Images.Any()) _context.ProductImages.RemoveRange(product.Images);
            if (product.Variants.Any()) _context.ProductVariants.RemoveRange(product.Variants);

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return true;
        }

        public async Task<bool> UpdateStockAsync(int id, int addedQuantity, int? variantId = null)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return false;

            if (variantId.HasValue && product.Variants.Any())
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == variantId.Value);
                if (variant == null) return false;
                variant.StockQuantity = Math.Max(0, variant.StockQuantity + addedQuantity);
                product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);
            }
            else if (product.Variants.Any())
            {
                if (addedQuantity >= 0)
                {
                    var targetVariant = product.Variants.OrderBy(v => v.StockQuantity).First();
                    targetVariant.StockQuantity += addedQuantity;
                }
                else
                {
                    var remainingToDeduct = -addedQuantity;
                    foreach (var v in product.Variants.OrderByDescending(v => v.StockQuantity))
                    {
                        if (remainingToDeduct <= 0) break;
                        var deduct = Math.Min(v.StockQuantity, remainingToDeduct);
                        v.StockQuantity -= deduct;
                        remainingToDeduct -= deduct;
                    }
                }
                product.StockQuantity = product.Variants.Sum(v => v.StockQuantity);
            }
            else
            {
                product.StockQuantity = Math.Max(0, product.StockQuantity + addedQuantity);
            }

            await _context.SaveChangesAsync();
            InvalidateCache();
            return true;
        }

        private IQueryable<Product> GetProductQuery() =>
            _context.Products
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Images.OrderBy(i => i.SortOrder))
                .Include(p => p.Specs.OrderBy(s => s.SortOrder))
                .Include(p => p.Highlights.OrderBy(h => h.SortOrder))
                .Include(p => p.Variants)
                .Include(p => p.Reviews).ThenInclude(r => r.User);

        private static ProductListDto MapToListDto(Product p) => new()
        {
            Id           = p.Id,
            ProductCode  = !string.IsNullOrWhiteSpace(p.ProductCode) ? p.ProductCode : $"SP-{p.Id:D3}",
            Name         = p.Name,
            Slug         = p.Slug,
            Price        = p.Price,
            SalePrice    = p.SalePrice,
            Badge        = p.Badge,
            IsActive     = p.IsActive,
            RatingAvg    = p.RatingAvg,
            ReviewCount  = p.Reviews.Count,
            StockQuantity= p.Variants.Any() ? p.Variants.Sum(v => v.StockQuantity) : p.StockQuantity,
            PrimaryImage = p.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                           ?? p.Images.FirstOrDefault()?.ImageUrl
                           ?? p.Variants.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.ImageUrl))?.ImageUrl ?? "",
            CategoryName = p.Category?.Name ?? "",
            CategorySlug = p.Category?.Slug ?? "",
            BrandName    = p.Brand?.Name ?? "",
            CategoryId   = p.CategoryId,
            BrandId      = p.BrandId,
            Description  = p.Description ?? "",
            Images       = p.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
            Highlights   = p.Highlights.OrderBy(h => h.SortOrder).Select(h => h.Content).ToList(),
            Specs        = p.Specs.OrderBy(s => s.SortOrder).Select(s => new ProductSpecDto { Key = s.SpecKey, Value = s.SpecValue }).ToList(),
            Variants     = p.Variants.Select(v => new ProductVariantDto
            {
                Id            = v.Id,
                ProductId     = v.ProductId,
                VariantName   = v.VariantName,
                ColorName     = v.ColorName,
                ColorCode     = v.ColorCode,
                Price         = v.Price,
                OriginalPrice = v.OriginalPrice,
                StockQuantity = v.StockQuantity,
                Sku           = v.Sku,
                ImageUrl      = v.ImageUrl
            }).ToList()
        };

        private static ProductDetailDto MapToDetailDto(Product p) => new()
        {
            Id           = p.Id,
            ProductCode  = !string.IsNullOrWhiteSpace(p.ProductCode) ? p.ProductCode : $"SP-{p.Id:D3}",
            Name         = p.Name,
            Slug         = p.Slug,
            Price        = p.Price,
            SalePrice    = p.SalePrice,
            Badge        = p.Badge,
            IsActive     = p.IsActive,
            RatingAvg    = p.RatingAvg,
            ReviewCount  = p.Reviews.Count,
            StockQuantity= p.Variants.Any() ? p.Variants.Sum(v => v.StockQuantity) : p.StockQuantity,
            PrimaryImage = p.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                           ?? p.Images.FirstOrDefault()?.ImageUrl
                           ?? p.Variants.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.ImageUrl))?.ImageUrl ?? "",
            CategoryName = p.Category?.Name ?? "",
            CategorySlug = p.Category?.Slug ?? "",
            BrandName    = p.Brand?.Name ?? "",
            CategoryId   = p.CategoryId,
            BrandId      = p.BrandId,
            Description  = p.Description ?? "",
            Images       = p.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
            Highlights   = p.Highlights.OrderBy(h => h.SortOrder).Select(h => h.Content).ToList(),
            Specs        = p.Specs.OrderBy(s => s.SortOrder).Select(s => new ProductSpecDto { Key = s.SpecKey, Value = s.SpecValue }).ToList(),
            Variants     = p.Variants.Select(v => new ProductVariantDto
            {
                Id            = v.Id,
                ProductId     = v.ProductId,
                VariantName   = v.VariantName,
                ColorName     = v.ColorName,
                ColorCode     = v.ColorCode,
                Price         = v.Price,
                OriginalPrice = v.OriginalPrice,
                StockQuantity = v.StockQuantity,
                Sku           = v.Sku,
                ImageUrl      = v.ImageUrl
            }).ToList(),
            Reviews      = p.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new ReviewDto
            {
                Id        = r.Id,
                ProductId = r.ProductId,
                OrderId   = r.OrderId,
                Username  = r.User?.FullName ?? r.User?.Username ?? "Ẩn danh",
                Rating    = r.Rating,
                Comment   = r.Comment,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
