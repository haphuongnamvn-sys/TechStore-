using Microsoft.EntityFrameworkCore;
using Web.Models;

namespace Web.Data
{
    /// <summary>
    /// Đồng bộ phiên bản / màu sắc / tồn kho theo từng biến thể và ảnh theo màu sắc cho sản phẩm.
    /// - Chạy lại an toàn mỗi lần khởi động (idempotent).
    /// - Ảnh màu được đọc từ wwwroot/images/products/p{id}-{slug}.(jpg|png|webp); màu nào chưa có ảnh
    ///   sẽ tự dùng ảnh đại diện của sản phẩm.
    /// </summary>
    public static class ProductCatalogSeeder
    {
        private const string SkuMarker = "V3-";
        private static readonly string[] Exts = { ".jpg", ".jpeg", ".png", ".webp" };

        // Ảnh tạm cho các sản phẩm chưa có file ảnh riêng trong wwwroot/images/products
        private static readonly Dictionary<int, string> InterimImages = new()
        {
            [8]  = "/images/products/p8-trang.jpg",
            [13] = "/images/products/p1-titan-tu-nhien.jpg",
            [22] = "/images/products/p22-den.jpg",
            [23] = "/images/products/p23-bac.jpg",
            [34] = "/images/products/p6-xam-khong-gian.jpg",
        };

        public static void Seed(AppDbContext db, string webRootPath)
        {
            try
            {
                RebuildVariantsIfNeeded(db);
                SyncImages(db, webRootPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ProductCatalogSeeder] " + ex.Message);
            }
        }

        private static void RebuildVariantsIfNeeded(AppDbContext db)
        {
            if (db.ProductVariants.Any(v => v.Sku != null && v.Sku.StartsWith(SkuMarker))) return;

            db.ProductVariants.RemoveRange(db.ProductVariants.ToList());
            db.SaveChanges();

            var products = db.Products.ToList();
            foreach (var p in products)
            {
                if (!ProductCatalog.Entries.TryGetValue(p.Id, out var entry)) continue;

                var basePrice = p.SalePrice > 0 ? p.SalePrice : p.Price;
                var origBase = p.Price > basePrice ? p.Price : basePrice;
                int total = 0;

                for (int vi = 0; vi < entry.Versions.Length; vi++)
                {
                    var ver = entry.Versions[vi];
                    for (int ci = 0; ci < entry.Colors.Length; ci++)
                    {
                        var col = entry.Colors[ci];
                        // Tồn kho khác nhau theo từng cặp (phiên bản, màu)
                        var stock = ((p.Id * 7 + vi * 5 + ci * 3) % 14) + 2;
                        total += stock;

                        db.ProductVariants.Add(new ProductVariant
                        {
                            ProductId = p.Id,
                            VariantName = ver.Name,
                            ColorName = col.Name,
                            ColorCode = col.Hex,
                            Price = basePrice + ver.Delta,
                            OriginalPrice = origBase + ver.Delta,
                            StockQuantity = stock,
                            Sku = $"{SkuMarker}{p.Id}-{vi + 1}-{col.Slug}"
                        });
                    }
                }

                // Tồn kho sản phẩm = tổng tồn kho các biến thể
                p.StockQuantity = total;
            }
            db.SaveChanges();
        }

        private static string? FindColorImage(string webRootPath, int imageProductId, string slug)
        {
            foreach (var ext in Exts)
            {
                var rel = $"images/products/p{imageProductId}-{slug}{ext}";
                if (File.Exists(Path.Combine(webRootPath, rel.Replace('/', Path.DirectorySeparatorChar))))
                    return "/" + rel;
            }
            return null;
        }

        private static void SyncImages(AppDbContext db, string webRootPath)
        {
            var products = db.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .ToList();

            foreach (var p in products)
            {
                if (!ProductCatalog.Entries.TryGetValue(p.Id, out var entry)) continue;
                var imgPid = entry.ImageProductId ?? p.Id;

                // slug -> url (chỉ những màu đã có ảnh thật)
                var colorImages = new List<(ColorDef Color, string Url)>();
                foreach (var c in entry.Colors)
                {
                    var url = FindColorImage(webRootPath, imgPid, c.Slug);
                    if (url != null) colorImages.Add((c, url));
                }

                // 1) Ảnh biến thể theo màu (chỉ gán mặc định nếu biến thể chưa có ảnh hoặc đang dùng ảnh tạm)
                foreach (var v in p.Variants)
                {
                    if (string.IsNullOrWhiteSpace(v.ImageUrl) || v.ImageUrl.Contains("unsplash.com"))
                    {
                        var match = colorImages.FirstOrDefault(ci => ci.Color.Name == v.ColorName);
                        if (match.Url != null) v.ImageUrl = match.Url;
                    }
                }

                // 2) Thư viện ảnh sản phẩm = khởi tạo nếu chưa có ảnh hoặc vẫn đang dùng ảnh mẫu unsplash cũ
                var galleryUrls = colorImages.Select(c => c.Url).ToList();
                if (galleryUrls.Count == 0 && InterimImages.TryGetValue(p.Id, out var interim))
                    galleryUrls.Add(interim);

                bool shouldInitGallery = !p.Images.Any() || p.Images.All(i => i.ImageUrl.Contains("unsplash.com"));
                if (galleryUrls.Count > 0 && shouldInitGallery)
                {
                    db.ProductImages.RemoveRange(p.Images);
                    for (int i = 0; i < galleryUrls.Count; i++)
                    {
                        db.ProductImages.Add(new ProductImage
                        {
                            ProductId = p.Id,
                            ImageUrl = galleryUrls[i],
                            IsPrimary = i == 0,
                            SortOrder = i
                        });
                    }
                }
            }
            db.SaveChanges();
        }
    }
}
