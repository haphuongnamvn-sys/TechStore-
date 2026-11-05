using Microsoft.EntityFrameworkCore;
using Web.Models;

namespace Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Brand> Brands { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!;
        public DbSet<ProductSpec> ProductSpecs { get; set; } = null!;
        public DbSet<ProductHighlight> ProductHighlights { get; set; } = null!;
        public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Promotion> Promotions { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<ChatConversation> ChatConversations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Role).HasDefaultValue("customer");
            });

            // Category (self-referencing) modelBuilder.Entity<Category>(e =>
            {
                e.HasOne(c => c.Parent)
                 .WithMany(c => c.Children)
                 .HasForeignKey(c => c.ParentId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(c => c.Slug).IsUnique();
            });

            // Product modelBuilder.Entity<Product>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.Property(p => p.Price).HasColumnType("decimal(18,2)");
                e.Property(p => p.SalePrice).HasColumnType("decimal(18,2)");
                e.HasOne(p => p.Category)
                 .WithMany(c => c.Products)
                 .HasForeignKey(p => p.CategoryId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.Brand)
                 .WithMany(b => b.Products)
                 .HasForeignKey(p => p.BrandId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ProductVariant modelBuilder.Entity<ProductVariant>(e =>
            {
                e.Property(v => v.Price).HasColumnType("decimal(18,2)");
                e.Property(v => v.OriginalPrice).HasColumnType("decimal(18,2)");
                e.HasOne(v => v.Product)
                 .WithMany(p => p.Variants)
                 .HasForeignKey(v => v.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ProductHighlight modelBuilder.Entity<ProductHighlight>(e =>
            {
                e.HasOne(h => h.Product)
                 .WithMany(p => p.Highlights)
                 .HasForeignKey(h => h.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Order modelBuilder.Entity<Order>(e =>
            {
                e.HasIndex(o => o.OrderCode).IsUnique();
                e.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
                e.HasOne(o => o.User)
                 .WithMany(u => u.Orders)
                 .HasForeignKey(o => o.UserId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // OrderItem modelBuilder.Entity<OrderItem>(e =>
            {
                e.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
                e.HasOne(i => i.Order)
                 .WithMany(o => o.OrderItems)
                 .HasForeignKey(i => i.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(i => i.Product)
                 .WithMany(p => p.OrderItems)
                 .HasForeignKey(i => i.ProductId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Payment modelBuilder.Entity<Payment>(e =>
            {
                e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                e.HasOne(p => p.Order)
                 .WithMany(o => o.Payments)
                 .HasForeignKey(p => p.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Cart modelBuilder.Entity<Cart>(e =>
            {
                e.HasOne(c => c.User)
                 .WithMany(u => u.CartItems)
                 .HasForeignKey(c => c.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Product)
                 .WithMany(p => p.CartItems)
                 .HasForeignKey(c => c.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Review modelBuilder.Entity<Review>(e =>
            {
                e.HasOne(r => r.User)
                 .WithMany(u => u.Reviews)
                 .HasForeignKey(r => r.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Product)
                 .WithMany(p => p.Reviews)
                 .HasForeignKey(r => r.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Order)
                 .WithMany()
                 .HasForeignKey(r => r.OrderId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // Promotion modelBuilder.Entity<Promotion>(e =>
            {
                e.HasIndex(p => p.Code).IsUnique();
                e.Property(p => p.DiscountValue).HasColumnType("decimal(18,2)");
                e.Property(p => p.MinOrderValue).HasColumnType("decimal(18,2)");
            });

            // ChatMessage modelBuilder.Entity<ChatMessage>(e =>
            {
                e.HasIndex(m => m.SessionId);
                e.HasIndex(m => m.CreatedAt);
                e.HasOne(m => m.User)
                 .WithMany()
                 .HasForeignKey(m => m.UserId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // SEED DATA

            // Users
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", FullName = "System Admin", Email = "admin@techstore.com", Password = "admin", Role = "admin", CreatedAt = new DateTime(2024, 1, 1) },
                new User { Id = 2, Username = "warehouse", FullName = "Warehouse Staff", Email = "warehouse@techstore.com", Password = "123456", Role = "warehouse", CreatedAt = new DateTime(2024, 1, 1) },
                new User { Id = 3, Username = "cskh", FullName = "NV Chăm Sóc Khách Hàng", Email = "cskh@techstore.com", Password = "123456", Role = "support", CreatedAt = new DateTime(2024, 1, 1) }
            );

            // Brands
            modelBuilder.Entity<Brand>().HasData(
                new Brand { Id = 1, Name = "Apple", LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/f/fa/Apple_logo_black.svg", Country = "USA" },
                new Brand { Id = 2, Name = "Samsung", LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/2/24/Samsung_Logo.svg", Country = "South Korea" },
                new Brand { Id = 3, Name = "Sony", LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/c/ca/Sony_logo.svg", Country = "Japan" },
                new Brand { Id = 4, Name = "ASUS", LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/2/2e/ASUS_Logo.svg", Country = "Taiwan" },
                new Brand { Id = 5, Name = "Xiaomi", LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/2/29/Xiaomi_logo.svg", Country = "China" },
                new Brand { Id = 6, Name = "Anker", LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/a/a0/Anker_logo.svg", Country = "USA" }
            );

            // Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Điện thoại", Slug = "dien-thoai", ImageUrl = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=200" },
                new Category { Id = 2, Name = "Laptop", Slug = "laptop", ImageUrl = "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=200" },
                new Category { Id = 3, Name = "Âm thanh", Slug = "am-thanh", ImageUrl = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=200" },
                new Category { Id = 4, Name = "Đồng hồ thông minh", Slug = "dong-ho-thong-minh", ImageUrl = "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?w=200" },
                new Category { Id = 5, Name = "Máy tính bảng", Slug = "may-tinh-bang", ImageUrl = "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=200" },
                new Category { Id = 6, Name = "Phụ kiện", Slug = "phu-kien", ImageUrl = "https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=200" }
            );

            // Products
            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1,  Name = "iPhone 15 Pro Max",        Slug = "iphone-15-pro-max",        Description = "Chip A17 Pro, màn hình Super Retina XDR 6.7\", titanium frame.",               Price = 34990000, SalePrice = 29990000, CategoryId = 1, BrandId = 1, StockQuantity = 50,  RatingAvg = 4.9, Badge = "sale",  CreatedAt = new DateTime(2024,1,1) },
                new Product { Id = 2,  Name = "Samsung Galaxy S24 Ultra", Slug = "samsung-galaxy-s24-ultra", Description = "Bút S-Pen tích hợp, camera 200MP, màn hình Dynamic AMOLED 6.8\".",              Price = 30990000, SalePrice = 26490000, CategoryId = 1, BrandId = 2, StockQuantity = 40,  RatingAvg = 4.8, Badge = "sale",  CreatedAt = new DateTime(2024,1,2) },
                new Product { Id = 3,  Name = "MacBook Air M3",           Slug = "macbook-air-m3",           Description = "Chip M3 mạnh mẽ, pin 18h, màn hình Liquid Retina 13.6\".",                      Price = 32000000, SalePrice = 28990000, CategoryId = 2, BrandId = 1, StockQuantity = 10,  RatingAvg = 4.9, Badge = "hot",   CreatedAt = new DateTime(2024,1,3) },
                new Product { Id = 4,  Name = "Sony WH-1000XM5",          Slug = "sony-wh-1000xm5",          Description = "Chống ồn ANC tốt nhất thị trường, âm thanh Hi-Res, pin 30h.",                   Price = 9990000,  SalePrice = 7490000,  CategoryId = 3, BrandId = 3, StockQuantity = 80,  RatingAvg = 4.8, Badge = "sale",  CreatedAt = new DateTime(2024,1,4) },
                new Product { Id = 5,  Name = "Apple Watch Series 9",     Slug = "apple-watch-series-9",     Description = "Màn hình Always-On Retina, S9 chip, đo oxy máu, ECG.",                          Price = 12990000, SalePrice = 9990000,  CategoryId = 4, BrandId = 1, StockQuantity = 30,  RatingAvg = 4.7, Badge = "sale",  CreatedAt = new DateTime(2024,1,5) },
                new Product { Id = 6,  Name = "iPad Pro M4",              Slug = "ipad-pro-m4",              Description = "Chip M4 cực mạnh, màn hình OLED Ultra Retina XDR 11\".",                        Price = 28000000, SalePrice = 24990000, CategoryId = 5, BrandId = 1, StockQuantity = 25,  RatingAvg = 4.9, Badge = "new",   CreatedAt = new DateTime(2024,1,6) },
                new Product { Id = 7,  Name = "ASUS ROG Zephyrus G14",   Slug = "asus-rog-zephyrus-g14",    Description = "Gaming laptop AMD Ryzen 9, RTX 4070, màn hình 2.5K 165Hz.",                      Price = 42000000, SalePrice = 35990000, CategoryId = 2, BrandId = 4, StockQuantity = 15,  RatingAvg = 4.7, Badge = "hot",   CreatedAt = new DateTime(2024,1,7) },
                new Product { Id = 8,  Name = "AirPods Pro 2nd Gen",      Slug = "airpods-pro-2nd-gen",      Description = "ANC thế hệ mới, Adaptive Audio, Transparency mode nâng cao.",                   Price = 7490000,  SalePrice = 5990000,  CategoryId = 3, BrandId = 1, StockQuantity = 100, RatingAvg = 4.8, Badge = "sale",  CreatedAt = new DateTime(2024,1,8) },
                new Product { Id = 9,  Name = "Samsung Galaxy Tab S9+",   Slug = "samsung-galaxy-tab-s9-plus",Description = "Màn hình Dynamic AMOLED 12.4\" 120Hz, chip Snapdragon 8 Gen 2.",               Price = 23990000, SalePrice = 19990000, CategoryId = 5, BrandId = 2, StockQuantity = 20,  RatingAvg = 4.7, Badge = "sale",  CreatedAt = new DateTime(2024,1,9) },
                new Product { Id = 10, Name = "Xiaomi Watch 2 Pro",       Slug = "xiaomi-watch-2-pro",       Description = "Wear OS, AMOLED 1.43\", Snapdragon W5+, GPS, SpO2, NFC.",                       Price = 4290000,  SalePrice = 3490000,  CategoryId = 4, BrandId = 5, StockQuantity = 60,  RatingAvg = 4.5, Badge = "sale",  CreatedAt = new DateTime(2024,1,10) },
                new Product { Id = 11, Name = "Dây sạc USB-C 140W",       Slug = "day-sac-usb-c-140w",       Description = "Sạc nhanh 140W, dài 1.8m, bọc dù cao cấp.",                                     Price = 399000,   SalePrice = 290000,   CategoryId = 6, BrandId = 6, StockQuantity = 200, RatingAvg = 4.6, Badge = "sale",  CreatedAt = new DateTime(2024,1,11) },
                new Product { Id = 12, Name = "Xiaomi 14 Ultra",          Slug = "xiaomi-14-ultra",          Description = "Camera Leica Professional, Snapdragon 8 Gen 3.",                                 Price = 25990000, SalePrice = 21990000, CategoryId = 1, BrandId = 5, StockQuantity = 35,  RatingAvg = 4.7, Badge = "new",   CreatedAt = new DateTime(2024,1,12) },

                // Thêm sản phẩm mới // Điện thoại
                new Product { Id = 13, Name = "iPhone 16 Pro Max",         Slug = "iphone-16-pro-max",         Description = "Chip A18 Pro, Camera Control, màn hình 6.9\" ProMotion 120Hz, titanium.",        Price = 39990000, SalePrice = 36990000, CategoryId = 1, BrandId = 1, StockQuantity = 45, RatingAvg = 4.9, Badge = "new",  CreatedAt = new DateTime(2024,9,20) },
                new Product { Id = 14, Name = "Samsung Galaxy S25 Ultra",   Slug = "samsung-galaxy-s25-ultra",   Description = "Snapdragon 8 Elite, S-Pen, camera 200MP, màn hình Dynamic AMOLED 6.9\" 120Hz.", Price = 33990000, SalePrice = 30490000, CategoryId = 1, BrandId = 2, StockQuantity = 38, RatingAvg = 4.8, Badge = "new",  CreatedAt = new DateTime(2025,1,22) },
                new Product { Id = 15, Name = "Xiaomi 15 Pro",              Slug = "xiaomi-15-pro",              Description = "Snapdragon 8 Elite, camera Leica Summilux, sạc 90W HyperCharge.",               Price = 22990000, SalePrice = 19990000, CategoryId = 1, BrandId = 5, StockQuantity = 50, RatingAvg = 4.7, Badge = "sale", CreatedAt = new DateTime(2024,10,29) },
                new Product { Id = 16, Name = "Samsung Galaxy A55 5G",      Slug = "samsung-galaxy-a55-5g",      Description = "Exynos 1480, màn hình Super AMOLED 6.6\" 120Hz, camera 50MP OIS, IP67.",        Price = 10990000, SalePrice = 9490000,  CategoryId = 1, BrandId = 2, StockQuantity = 90, RatingAvg = 4.5, Badge = "sale", CreatedAt = new DateTime(2024,3,11) },
                new Product { Id = 17, Name = "iPhone 15",                  Slug = "iphone-15",                  Description = "Chip A16 Bionic, Dynamic Island, camera 48MP, màn hình 6.1\" Super Retina.",    Price = 22990000, SalePrice = 19490000, CategoryId = 1, BrandId = 1, StockQuantity = 60, RatingAvg = 4.7, Badge = "sale", CreatedAt = new DateTime(2023,9,22) },

                // Laptop
                new Product { Id = 18, Name = "MacBook Pro M4 Pro 14\"",   Slug = "macbook-pro-m4-pro-14",      Description = "Chip M4 Pro 12-core CPU, Liquid Retina XDR 14.2\", pin 24h.",                   Price = 55990000, SalePrice = 52990000, CategoryId = 2, BrandId = 1, StockQuantity = 12, RatingAvg = 4.9, Badge = "new",  CreatedAt = new DateTime(2024,11,8) },
                new Product { Id = 19, Name = "ASUS Vivobook 15 OLED",     Slug = "asus-vivobook-15-oled",      Description = "Intel Core i5-13500H, OLED 2.8K 120Hz, RAM 16GB, SSD 512GB.",                  Price = 18490000, SalePrice = 15990000, CategoryId = 2, BrandId = 4, StockQuantity = 25, RatingAvg = 4.5, Badge = "sale", CreatedAt = new DateTime(2024,2,15) },
                new Product { Id = 20, Name = "Lenovo ThinkPad X1 Carbon", Slug = "lenovo-thinkpad-x1-carbon",  Description = "Intel Core Ultra 7, màn hình IPS 14\" 2.8K, trọng lượng 1.12kg, pin 15h.",      Price = 42000000, SalePrice = 37990000, CategoryId = 2, BrandId = 4, StockQuantity = 8,  RatingAvg = 4.8, Badge = "hot",  CreatedAt = new DateTime(2024,5,1) },
                new Product { Id = 21, Name = "ASUS TUF Gaming A15",       Slug = "asus-tuf-gaming-a15",        Description = "AMD Ryzen 7 7745HX, RTX 4060 8GB, RAM 16GB, màn hình 15.6\" FHD 144Hz.",        Price = 24990000, SalePrice = 21490000, CategoryId = 2, BrandId = 4, StockQuantity = 20, RatingAvg = 4.6, Badge = "sale", CreatedAt = new DateTime(2024,4,10) },

                // Âm thanh
                new Product { Id = 22, Name = "Sony WF-1000XM5",           Slug = "sony-wf-1000xm5",            Description = "TWS ANC tốt nhất, LDAC Hi-Res, pin 8h + 16h case, chống nước IPX4.",           Price = 7490000,  SalePrice = 5990000,  CategoryId = 3, BrandId = 3, StockQuantity = 70, RatingAvg = 4.8, Badge = "sale", CreatedAt = new DateTime(2023,7,1) },
                new Product { Id = 23, Name = "Samsung Galaxy Buds3 Pro",  Slug = "samsung-galaxy-buds3-pro",   Description = "ANC thích ứng, chất âm Hi-Fi 24-bit, chống nước IP57, pin 6h.",                 Price = 5490000,  SalePrice = 4290000,  CategoryId = 3, BrandId = 2, StockQuantity = 85, RatingAvg = 4.6, Badge = "sale", CreatedAt = new DateTime(2024,7,10) },
                new Product { Id = 24, Name = "Anker Soundcore Q45",       Slug = "anker-soundcore-q45",        Description = "Chống ồn ANC 50dB, Bluetooth 5.3, pin 50h, kết nối đa thiết bị.",              Price = 1990000,  SalePrice = 1490000,  CategoryId = 3, BrandId = 6, StockQuantity = 120, RatingAvg = 4.5, Badge = "sale", CreatedAt = new DateTime(2023,10,1) },

                // Đồng hồ thông minh
                new Product { Id = 25, Name = "Apple Watch Ultra 2",       Slug = "apple-watch-ultra-2",        Description = "Titanium, màn hình Retina 2000 nits, GPS L5, chống nước 100m, pin 36h.",        Price = 23990000, SalePrice = 21490000, CategoryId = 4, BrandId = 1, StockQuantity = 18, RatingAvg = 4.9, Badge = "hot",  CreatedAt = new DateTime(2023,9,22) },
                new Product { Id = 26, Name = "Samsung Galaxy Watch 7",    Slug = "samsung-galaxy-watch-7",     Description = "Exynos W1000, BioActive Sensor, màn hình Super AMOLED 1.5\", Wear OS.",         Price = 7990000,  SalePrice = 6490000,  CategoryId = 4, BrandId = 2, StockQuantity = 42, RatingAvg = 4.6, Badge = "sale", CreatedAt = new DateTime(2024,7,24) },

                // Máy tính bảng
                new Product { Id = 27, Name = "iPad Air M2 11\"",          Slug = "ipad-air-m2-11",             Description = "Chip M2, màn hình Liquid Retina 11\" 60Hz, camera 12MP, USB-C 3.0.",            Price = 18990000, SalePrice = 16990000, CategoryId = 5, BrandId = 1, StockQuantity = 28, RatingAvg = 4.8, Badge = "sale", CreatedAt = new DateTime(2024,5,15) },
                new Product { Id = 28, Name = "Xiaomi Pad 6S Pro",         Slug = "xiaomi-pad-6s-pro",          Description = "Snapdragon 8 Gen 2, màn hình 12.4\" 144Hz WQHD+, sạc 67W.",                   Price = 14990000, SalePrice = 12490000, CategoryId = 5, BrandId = 5, StockQuantity = 32, RatingAvg = 4.6, Badge = "sale", CreatedAt = new DateTime(2024,2,25) },

                // Phụ kiện
                new Product { Id = 29, Name = "Anker MagSafe Charger 15W", Slug = "anker-magsafe-charger-15w",  Description = "Sạc không dây MagSafe 15W cho iPhone 12-16, kèm cáp USB-C 1.5m.",              Price = 690000,   SalePrice = 490000,   CategoryId = 6, BrandId = 6, StockQuantity = 180, RatingAvg = 4.6, Badge = "sale", CreatedAt = new DateTime(2023,12,1) },
                new Product { Id = 30, Name = "Anker PowerBank 24000mAh",  Slug = "anker-powerbank-24000mah",   Description = "24000mAh, sạc nhanh 65W PD, 3 cổng (2 USB-C + 1 USB-A), tương thích laptop.", Price = 1890000,  SalePrice = 1490000,  CategoryId = 6, BrandId = 6, StockQuantity = 95,  RatingAvg = 4.7, Badge = "sale", CreatedAt = new DateTime(2024,1,20) },
                new Product { Id = 31, Name = "Ốp lưng iPhone 16 Pro Max", Slug = "op-lung-iphone-16-pro-max",  Description = "Silicon cao cấp chống sốc, hỗ trợ MagSafe, bảo vệ camera.",                   Price = 350000,   SalePrice = 199000,   CategoryId = 6, BrandId = 1, StockQuantity = 500, RatingAvg = 4.4, Badge = "sale", CreatedAt = new DateTime(2024,9,25) },
                new Product { Id = 32, Name = "Kính cường lực iPhone 16",  Slug = "kinh-cuong-luc-iphone-16",   Description = "Kính cường lực 9H, full màn hình, chống vân tay, bộ 2 miếng.",                  Price = 199000,   SalePrice = 99000,    CategoryId = 6, BrandId = 6, StockQuantity = 600, RatingAvg = 4.3, Badge = "sale", CreatedAt = new DateTime(2024,9,25) }
            );

            // ProductImages (primary images for all products)
            modelBuilder.Entity<ProductImage>().HasData(
                new ProductImage { Id = 1,  ProductId = 1,  ImageUrl = "https://images.unsplash.com/photo-1695048133142-1a20484d2569?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 2,  ProductId = 1,  ImageUrl = "https://images.unsplash.com/photo-1632661674596-df8be070a5c5?w=600", IsPrimary = false, SortOrder = 1 },
                new ProductImage { Id = 3,  ProductId = 2,  ImageUrl = "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 4,  ProductId = 3,  ImageUrl = "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 5,  ProductId = 4,  ImageUrl = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 6,  ProductId = 5,  ImageUrl = "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 7,  ProductId = 6,  ImageUrl = "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 8,  ProductId = 7,  ImageUrl = "https://images.unsplash.com/photo-1603302576837-37561b2e2302?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 9,  ProductId = 8,  ImageUrl = "https://images.unsplash.com/photo-1588449668338-d15178222883?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 10, ProductId = 9,  ImageUrl = "https://images.unsplash.com/photo-1561154464-82e9adf32764?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 11, ProductId = 10, ImageUrl = "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 12, ProductId = 11, ImageUrl = "https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 13, ProductId = 12, ImageUrl = "https://images.unsplash.com/photo-1598327105666-5b89351aff97?w=600", IsPrimary = true,  SortOrder = 0 },
                // New products
                new ProductImage { Id = 14, ProductId = 13, ImageUrl = "https://images.unsplash.com/photo-1726878778148-7b4b6f3fef71?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 15, ProductId = 14, ImageUrl = "https://images.unsplash.com/photo-1715017458660-db8cc73b0765?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 16, ProductId = 15, ImageUrl = "https://images.unsplash.com/photo-1598327105666-5b89351aff97?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 17, ProductId = 16, ImageUrl = "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 18, ProductId = 17, ImageUrl = "https://images.unsplash.com/photo-1695048133142-1a20484d2569?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 19, ProductId = 18, ImageUrl = "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 20, ProductId = 19, ImageUrl = "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 21, ProductId = 20, ImageUrl = "https://images.unsplash.com/photo-1593642632559-0c6d3fc62b89?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 22, ProductId = 21, ImageUrl = "https://images.unsplash.com/photo-1603302576837-37561b2e2302?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 23, ProductId = 22, ImageUrl = "https://images.unsplash.com/photo-1590658268037-6bf12165a8df?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 24, ProductId = 23, ImageUrl = "https://images.unsplash.com/photo-1606220838315-056192d5e927?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 25, ProductId = 24, ImageUrl = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 26, ProductId = 25, ImageUrl = "https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 27, ProductId = 26, ImageUrl = "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 28, ProductId = 27, ImageUrl = "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 29, ProductId = 28, ImageUrl = "https://images.unsplash.com/photo-1561154464-82e9adf32764?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 30, ProductId = 29, ImageUrl = "https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 31, ProductId = 30, ImageUrl = "https://images.unsplash.com/photo-1609091839311-d5365f9ff1c5?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 32, ProductId = 31, ImageUrl = "https://images.unsplash.com/photo-1601784551446-20c9e07cdbdb?w=600", IsPrimary = true,  SortOrder = 0 },
                new ProductImage { Id = 33, ProductId = 32, ImageUrl = "https://images.unsplash.com/photo-1573739022854-abceaeb585dc?w=600", IsPrimary = true,  SortOrder = 0 }
            );

            // ProductSpecs
            modelBuilder.Entity<ProductSpec>().HasData(
                // iPhone 15 Pro Max
                new ProductSpec { Id = 1,  ProductId = 1,  SpecKey = "Chip",          SpecValue = "Apple A17 Pro" },
                new ProductSpec { Id = 2,  ProductId = 1,  SpecKey = "Màn hình",      SpecValue = "6.7\" Super Retina XDR OLED" },
                new ProductSpec { Id = 3,  ProductId = 1,  SpecKey = "Camera sau",    SpecValue = "48MP + 12MP + 12MP" },
                new ProductSpec { Id = 4,  ProductId = 1,  SpecKey = "RAM",           SpecValue = "8GB" },
                new ProductSpec { Id = 5,  ProductId = 1,  SpecKey = "Bộ nhớ",       SpecValue = "256GB" },
                // MacBook Air M3
                new ProductSpec { Id = 6,  ProductId = 3,  SpecKey = "Chip",          SpecValue = "Apple M3 8-core CPU" },
                new ProductSpec { Id = 7,  ProductId = 3,  SpecKey = "RAM",           SpecValue = "16GB Unified Memory" },
                new ProductSpec { Id = 8,  ProductId = 3,  SpecKey = "Ổ cứng",       SpecValue = "512GB SSD" },
                new ProductSpec { Id = 9,  ProductId = 3,  SpecKey = "Pin",           SpecValue = "18 giờ" },
                // Sony WH-1000XM5
                new ProductSpec { Id = 10, ProductId = 4,  SpecKey = "Driver",        SpecValue = "30mm" },
                new ProductSpec { Id = 11, ProductId = 4,  SpecKey = "Kết nối",      SpecValue = "Bluetooth 5.2" },
                new ProductSpec { Id = 12, ProductId = 4,  SpecKey = "Pin",           SpecValue = "30 giờ" },
                new ProductSpec { Id = 13, ProductId = 4,  SpecKey = "Chống ồn",     SpecValue = "ANC thế hệ mới" },
                // ASUS ROG Zephyrus G14
                new ProductSpec { Id = 14, ProductId = 7,  SpecKey = "CPU",           SpecValue = "AMD Ryzen 9 8945HS" },
                new ProductSpec { Id = 15, ProductId = 7,  SpecKey = "GPU",           SpecValue = "NVIDIA RTX 4070" },
                new ProductSpec { Id = 16, ProductId = 7,  SpecKey = "RAM",           SpecValue = "32GB DDR5" },
                new ProductSpec { Id = 17, ProductId = 7,  SpecKey = "Màn hình",     SpecValue = "14\" 2.5K 165Hz" }
            );

            // Promotions
            modelBuilder.Entity<Promotion>().HasData(
                new Promotion { Id = 1, Code = "WELCOME10", DiscountType = "percent", DiscountValue = 10, MinOrderValue = 500000,  MaxUsage = 100, UsedCount = 0, StartDate = new DateTime(2024,1,1), EndDate = new DateTime(2025,12,31) },
                new Promotion { Id = 2, Code = "SALE200K",  DiscountType = "fixed",   DiscountValue = 200000, MinOrderValue = 2000000, MaxUsage = 50,  UsedCount = 0, StartDate = new DateTime(2024,1,1), EndDate = new DateTime(2025,12,31) }
            );

            // ProductVariants
            modelBuilder.Entity<ProductVariant>().HasData(
                // iPhone 15 Pro Max (ProductId = 1)
                new ProductVariant { Id = 1, ProductId = 1, VariantName = "256GB", ColorName = "Titan Tự Nhiên", ColorCode = "#b3aca4", Price = 29990000, OriginalPrice = 34990000, StockQuantity = 20, Sku = "IP15PM-256-NAT" },
                new ProductVariant { Id = 2, ProductId = 1, VariantName = "256GB", ColorName = "Titan Đen", ColorCode = "#3b3c3e", Price = 29990000, OriginalPrice = 34990000, StockQuantity = 15, Sku = "IP15PM-256-BLK" },
                new ProductVariant { Id = 3, ProductId = 1, VariantName = "256GB", ColorName = "Titan Trắng", ColorCode = "#e3e4e6", Price = 29990000, OriginalPrice = 34990000, StockQuantity = 10, Sku = "IP15PM-256-WHT" },
                new ProductVariant { Id = 4, ProductId = 1, VariantName = "256GB", ColorName = "Titan Xanh", ColorCode = "#313945", Price = 29990000, OriginalPrice = 34990000, StockQuantity = 5, Sku = "IP15PM-256-BLU" },
                new ProductVariant { Id = 5, ProductId = 1, VariantName = "512GB", ColorName = "Titan Tự Nhiên", ColorCode = "#b3aca4", Price = 34990000, OriginalPrice = 39990000, StockQuantity = 12, Sku = "IP15PM-512-NAT" },
                new ProductVariant { Id = 6, ProductId = 1, VariantName = "512GB", ColorName = "Titan Đen", ColorCode = "#3b3c3e", Price = 34990000, OriginalPrice = 39990000, StockQuantity = 8, Sku = "IP15PM-512-BLK" },
                new ProductVariant { Id = 7, ProductId = 1, VariantName = "1TB", ColorName = "Titan Tự Nhiên", ColorCode = "#b3aca4", Price = 41990000, OriginalPrice = 46990000, StockQuantity = 6, Sku = "IP15PM-1TB-NAT" },
                new ProductVariant { Id = 8, ProductId = 1, VariantName = "1TB", ColorName = "Titan Đen", ColorCode = "#3b3c3e", Price = 41990000, OriginalPrice = 46990000, StockQuantity = 4, Sku = "IP15PM-1TB-BLK" },

                // Samsung Galaxy S24 Ultra (ProductId = 2)
                new ProductVariant { Id = 9, ProductId = 2, VariantName = "256GB", ColorName = "Xám Titan", ColorCode = "#7a7b80", Price = 26490000, OriginalPrice = 30990000, StockQuantity = 15, Sku = "S24U-256-GRY" },
                new ProductVariant { Id = 10, ProductId = 2, VariantName = "256GB", ColorName = "Đen Titan", ColorCode = "#2c2d30", Price = 26490000, OriginalPrice = 30990000, StockQuantity = 12, Sku = "S24U-256-BLK" },
                new ProductVariant { Id = 11, ProductId = 2, VariantName = "256GB", ColorName = "Tím Titan", ColorCode = "#534b62", Price = 26490000, OriginalPrice = 30990000, StockQuantity = 8, Sku = "S24U-256-PUR" },
                new ProductVariant { Id = 12, ProductId = 2, VariantName = "256GB", ColorName = "Vàng Titan", ColorCode = "#e5d8b8", Price = 26490000, OriginalPrice = 30990000, StockQuantity = 5, Sku = "S24U-256-YEL" },
                new ProductVariant { Id = 13, ProductId = 2, VariantName = "512GB", ColorName = "Xám Titan", ColorCode = "#7a7b80", Price = 30490000, OriginalPrice = 34990000, StockQuantity = 10, Sku = "S24U-512-GRY" },
                new ProductVariant { Id = 14, ProductId = 2, VariantName = "1TB", ColorName = "Xám Titan", ColorCode = "#7a7b80", Price = 36990000, OriginalPrice = 41990000, StockQuantity = 5, Sku = "S24U-1TB-GRY" },

                // MacBook Air M3 (ProductId = 3)
                new ProductVariant { Id = 15, ProductId = 3, VariantName = "8GB / 256GB", ColorName = "Midnight", ColorCode = "#1e2530", Price = 28990000, OriginalPrice = 32000000, StockQuantity = 5, Sku = "MBA-M3-8-256-MID" },
                new ProductVariant { Id = 16, ProductId = 3, VariantName = "8GB / 256GB", ColorName = "Starlight", ColorCode = "#e8e3d9", Price = 28990000, OriginalPrice = 32000000, StockQuantity = 4, Sku = "MBA-M3-8-256-STA" },
                new ProductVariant { Id = 17, ProductId = 3, VariantName = "8GB / 256GB", ColorName = "Space Gray", ColorCode = "#7d7e80", Price = 28990000, OriginalPrice = 32000000, StockQuantity = 5, Sku = "MBA-M3-8-256-GRY" },
                new ProductVariant { Id = 18, ProductId = 3, VariantName = "16GB / 512GB", ColorName = "Midnight", ColorCode = "#1e2530", Price = 35990000, OriginalPrice = 39000000, StockQuantity = 6, Sku = "MBA-M3-16-512-MID" },
                new ProductVariant { Id = 19, ProductId = 3, VariantName = "16GB / 512GB", ColorName = "Space Gray", ColorCode = "#7d7e80", Price = 35990000, OriginalPrice = 39000000, StockQuantity = 4, Sku = "MBA-M3-16-512-GRY" },

                // Sony WH-1000XM5 (ProductId = 4)
                new ProductVariant { Id = 20, ProductId = 4, VariantName = "Tiêu chuẩn", ColorName = "Đen", ColorCode = "#1a1a1a", Price = 7490000, OriginalPrice = 9990000, StockQuantity = 35, Sku = "WH1000XM5-BLK" },
                new ProductVariant { Id = 21, ProductId = 4, VariantName = "Tiêu chuẩn", ColorName = "Bạc", ColorCode = "#e2e0d8", Price = 7490000, OriginalPrice = 9990000, StockQuantity = 25, Sku = "WH1000XM5-SLV" },
                new ProductVariant { Id = 22, ProductId = 4, VariantName = "Tiêu chuẩn", ColorName = "Xanh Midnight", ColorCode = "#1d2a44", Price = 7490000, OriginalPrice = 9990000, StockQuantity = 20, Sku = "WH1000XM5-BLU" },

                // Apple Watch Series 9 (ProductId = 5)
                new ProductVariant { Id = 23, ProductId = 5, VariantName = "41mm GPS", ColorName = "Nhôm Midnight", ColorCode = "#1f242b", Price = 9990000, OriginalPrice = 12990000, StockQuantity = 15, Sku = "AW9-41-MID" },
                new ProductVariant { Id = 24, ProductId = 5, VariantName = "41mm GPS", ColorName = "Nhôm Starlight", ColorCode = "#eae6df", Price = 9990000, OriginalPrice = 12990000, StockQuantity = 10, Sku = "AW9-41-STA" },
                new ProductVariant { Id = 25, ProductId = 5, VariantName = "45mm GPS", ColorName = "Nhôm Midnight", ColorCode = "#1f242b", Price = 10990000, OriginalPrice = 13990000, StockQuantity = 10, Sku = "AW9-45-MID" },

                // iPhone 16 Pro Max (ProductId = 13)
                new ProductVariant { Id = 26, ProductId = 13, VariantName = "256GB", ColorName = "Titan Sa Mạc", ColorCode = "#c5a88c", Price = 36990000, OriginalPrice = 39990000, StockQuantity = 20, Sku = "IP16PM-256-DES" },
                new ProductVariant { Id = 27, ProductId = 13, VariantName = "256GB", ColorName = "Titan Tự Nhiên", ColorCode = "#b3aca4", Price = 36990000, OriginalPrice = 39990000, StockQuantity = 15, Sku = "IP16PM-256-NAT" },
                new ProductVariant { Id = 28, ProductId = 13, VariantName = "256GB", ColorName = "Titan Đen", ColorCode = "#242526", Price = 36990000, OriginalPrice = 39990000, StockQuantity = 12, Sku = "IP16PM-256-BLK" },
                new ProductVariant { Id = 29, ProductId = 13, VariantName = "256GB", ColorName = "Titan Trắng", ColorCode = "#edebe6", Price = 36990000, OriginalPrice = 39990000, StockQuantity = 10, Sku = "IP16PM-256-WHT" },
                new ProductVariant { Id = 30, ProductId = 13, VariantName = "512GB", ColorName = "Titan Sa Mạc", ColorCode = "#c5a88c", Price = 42990000, OriginalPrice = 45990000, StockQuantity = 8, Sku = "IP16PM-512-DES" },
                new ProductVariant { Id = 31, ProductId = 13, VariantName = "1TB", ColorName = "Titan Sa Mạc", ColorCode = "#c5a88c", Price = 48990000, OriginalPrice = 52990000, StockQuantity = 5, Sku = "IP16PM-1TB-DES" }
            );
        }
    }
}
