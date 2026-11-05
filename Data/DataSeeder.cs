using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Models;

namespace Web.Data
{
    /// <summary>
    /// Nâng cấp schema CSDL, khởi tạo danh mục, sản phẩm, thông số kỹ thuật theo từng loại sản phẩm
    /// và đặc điểm nổi bật riêng biệt cho từng sản phẩm.
    /// </summary>
    public static class DataSeeder
    {
        public static void SeedAdditionalProducts(AppDbContext db)
        {
            try
            {
                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsLocked')
                        ALTER TABLE [Users] ADD [IsLocked] BIT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Code')
                        ALTER TABLE [Categories] ADD [Code] NVARCHAR(50) NOT NULL DEFAULT '';

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Description')
                        ALTER TABLE [Categories] ADD [Description] NVARCHAR(500) NOT NULL DEFAULT '';

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'IsActive')
                        ALTER TABLE [Categories] ADD [IsActive] BIT NOT NULL DEFAULT 1;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'SpecTemplatesJson')
                        ALTER TABLE [Categories] ADD [SpecTemplatesJson] NVARCHAR(MAX) NOT NULL DEFAULT '';

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ProductCode')
                        ALTER TABLE [Products] ADD [ProductCode] NVARCHAR(50) NOT NULL DEFAULT '';

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'IsActive')
                        ALTER TABLE [Products] ADD [IsActive] BIT NOT NULL DEFAULT 1;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductSpecs') AND name = 'SortOrder')
                        ALTER TABLE [ProductSpecs] ADD [SortOrder] INT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductHighlights')
                    BEGIN
                        CREATE TABLE [ProductHighlights] (
                            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [ProductId] INT NOT NULL FOREIGN KEY REFERENCES [Products]([Id]) ON DELETE CASCADE,
                            [Content] NVARCHAR(1000) NOT NULL,
                            [SortOrder] INT NOT NULL DEFAULT 0
                        );
                        CREATE INDEX [IX_ProductHighlights_ProductId] ON [ProductHighlights]([ProductId]);
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessages')
                    BEGIN
                        CREATE TABLE [ChatMessages] (
                            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [UserId] INT NULL FOREIGN KEY REFERENCES [Users]([Id]) ON DELETE SET NULL,
                            [SessionId] NVARCHAR(100) NOT NULL,
                            [Sender] NVARCHAR(20) NOT NULL,
                            [SenderName] NVARCHAR(100) NULL,
                            [Message] NVARCHAR(MAX) NOT NULL,
                            [IsStaffReply] BIT NOT NULL DEFAULT 0,
                            [IsRead] BIT NOT NULL DEFAULT 0,
                            [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                        CREATE INDEX [IX_ChatMessages_SessionId] ON [ChatMessages]([SessionId]);
                        CREATE INDEX [IX_ChatMessages_CreatedAt] ON [ChatMessages]([CreatedAt]);
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatConversations')
                    BEGIN
                        CREATE TABLE [ChatConversations] (
                            [SessionId] NVARCHAR(100) NOT NULL PRIMARY KEY,
                            [CustomerUserId] INT NULL,
                            [CustomerName] NVARCHAR(100) NULL,
                            [CustomerPhone] NVARCHAR(50) NULL,
                            [AssignedStaffId] INT NULL,
                            [AssignedStaffName] NVARCHAR(100) NULL,
                            [Status] NVARCHAR(20) NOT NULL DEFAULT 'waiting',
                            [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            [AssignedAt] DATETIME2 NULL,
                            [ClosedAt] DATETIME2 NULL
                        );
                        CREATE INDEX [IX_ChatConversations_Status] ON [ChatConversations]([Status]);
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductVariants')
                    BEGIN
                        CREATE TABLE [ProductVariants] (
                            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [ProductId] INT NOT NULL FOREIGN KEY REFERENCES [Products]([Id]) ON DELETE CASCADE,
                            [VariantName] NVARCHAR(100) NOT NULL,
                            [ColorName] NVARCHAR(100) NOT NULL,
                            [ColorCode] NVARCHAR(50) NULL,
                            [Price] DECIMAL(18,2) NOT NULL,
                            [OriginalPrice] DECIMAL(18,2) NULL,
                            [StockQuantity] INT NOT NULL DEFAULT 0,
                            [Sku] NVARCHAR(100) NULL,
                            [ImageUrl] NVARCHAR(500) NULL
                        );
                        CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants]([ProductId]);
                    END

                    IF NOT EXISTS (SELECT * FROM [Users] WHERE [Username] = 'cskh')
                    BEGIN
                        INSERT INTO [Users] ([Username], [FullName], [Email], [Password], [Role], [Phone], [Address], [IsLocked], [CreatedAt])
                        VALUES ('cskh', N'Lê Minh (CSKH 1)', 'cskh@techstore.com', '123456', 'support', '0988666888', N'Hà Nội', 0, SYSUTCDATETIME());
                    END
                    ELSE
                    BEGIN
                        UPDATE [Users] SET [Role] = 'support' WHERE [Username] = 'cskh';
                    END

                    IF NOT EXISTS (SELECT * FROM [Users] WHERE [Username] = 'cskh2')
                    BEGIN
                        INSERT INTO [Users] ([Username], [FullName], [Email], [Password], [Role], [Phone], [Address], [IsLocked], [CreatedAt])
                        VALUES ('cskh2', N'Trần Mai (CSKH 2)', 'cskh2@techstore.com', '123456', 'support', '0912345678', N'Hà Nội', 0, SYSUTCDATETIME());
                    END

                    IF NOT EXISTS (SELECT * FROM [Users] WHERE [Username] = 'customer')
                    BEGIN
                        INSERT INTO [Users] ([Username], [FullName], [Email], [Password], [Role], [Phone], [Address], [IsLocked], [CreatedAt])
                        VALUES ('customer', N'Nguyễn Văn Khách', 'customer@techstore.com', '123456', 'customer', '0987654321', N'TP. Hồ Chí Minh', 0, SYSUTCDATETIME());
                    END
                ");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DataSeeder Schema] " + ex.Message);
            }

            SeedCategoryMetadata(db);

            var products = new[]
            {
                ("iPhone 16 Pro Max", "iphone-16-pro-max", "Chip Apple A18 Pro mạnh mẽ, nút Camera Control thế hệ mới, màn hình 6.9\" Super Retina XDR ProMotion 120Hz viền siêu mỏng, khung viền Titan cấp 5 sang trọng và bền bỉ.", 39990000m, 36990000m, 1, 1, 45, 4.9, "new", "/images/products/p13-sa-mac.jpg"),
                ("Samsung Galaxy S25 Ultra", "samsung-galaxy-s25-ultra", "Vi xử lý Snapdragon 8 Elite for Galaxy tối tân, camera mắt thần 200MP zoom quang 100x AI, bút S-Pen đa nhiệm đỉnh cao, màn hình phẳng Dynamic AMOLED 2X 6.9\" 120Hz chống chói.", 33990000m, 30490000m, 1, 2, 38, 4.8, "new", "/images/products/p14-xam-titan.jpg"),
                ("Xiaomi 15 Pro", "xiaomi-15-pro", "Hệ thống ống kính quang học Leica Summilux huyền thoại, cảm biến Light Fusion 900, chip Snapdragon 8 Elite tiến trình 3nm, sạc siêu tốc 90W HyperCharge và pin Silicon-Carbon 6100mAh.", 22990000m, 19990000m, 1, 5, 50, 4.7, "sale", "/images/products/p15-den.jpg"),
                ("Samsung Galaxy A55 5G", "samsung-galaxy-a55-5g", "Thiết kế viền kim loại cao cấp, màn hình Super AMOLED 6.6\" 120Hz mượt mà, vi xử lý Exynos 1480 cùng đồ họa AMD, chuẩn kháng nước bụi IP67 bền bỉ.", 10990000m, 9490000m, 1, 2, 90, 4.5, "sale", "/images/products/p16-xanh-navy.jpg"),
                ("iPhone 15", "iphone-15", "Màn hình Dynamic Island thông minh, camera chính 48MP bắt trọn khoảnh khắc chi tiết, chip A16 Bionic hiệu năng mượt mà vượt trội, cổng kết nối Type-C tiện lợi.", 22990000m, 19490000m, 1, 1, 60, 4.7, "sale", "/images/products/p17-hong.jpg"),
                ("MacBook Pro M4 Pro 14inch", "macbook-pro-m4-pro-14inch", "Chip Apple M4 Pro 12-core CPU và 16-core GPU, màn hình Liquid Retina XDR Nano-texture siêu nét, hỗ trợ kết nối Thunderbolt 5 tốc độ cao, thời lượng pin lên đến 24 giờ liên tục.", 55990000m, 52990000m, 2, 1, 12, 4.9, "new", "/images/products/p18-den-khong-gian.jpg"),
                ("ASUS Vivobook 15 OLED", "asus-vivobook-15-oled", "Màn hình OLED 2.8K 120Hz chuẩn màu điện ảnh 100% DCI-P3, vi xử lý Intel Core i5-13500H thế hệ mới, RAM 16GB đa nhiệm mượt, trọng lượng gọn nhẹ thời trang.", 18490000m, 15990000m, 2, 4, 25, 4.5, "sale", "/images/products/p19-bac.jpg"),
                ("ASUS TUF Gaming A15", "asus-tuf-gaming-a15", "Laptop chiến game chuẩn quân đội độ bền cao, chip AMD Ryzen 7 7745HX kết hợp card đồ họa RTX 4060 8GB GDDR6, màn hình 15.6\" FHD 144Hz chống giật xé hình.", 24990000m, 21490000m, 2, 4, 20, 4.6, "sale", "/images/products/p21-xam-mecha.jpg"),
                ("Sony WF-1000XM5", "sony-wf-1000xm5", "Tai nghe true wireless chống ồn chủ động đỉnh cao, bộ xử lý V2 và QN2e, màng loa Dynamic Driver X, âm thanh Hi-Res LDAC, thời lượng pin 24 giờ kèm hộp sạc.", 7490000m, 5990000m, 3, 3, 70, 4.8, "sale", "/images/products/p22-den.jpg"),
                ("Samsung Galaxy Buds3 Pro", "samsung-galaxy-buds3-pro", "Thiết kế công thái học hiện đại với dải đèn Blade Lights độc đáo, âm thanh Hi-Fi 24-bit sắc sảo, chống ồn thông minh theo môi trường sống, kháng nước kháng bụi chuẩn IP57.", 5490000m, 4290000m, 3, 2, 85, 4.6, "sale", "/images/products/p23-bac.jpg"),
                ("Anker Soundcore Q45", "anker-soundcore-q45", "Tai nghe chụp tai chống ồn chủ động thích ứng giảm 98% tiếng ồn, chứng nhận Hi-Res Audio Wireless qua codec LDAC, thời lượng pin khủng lên đến 50 giờ chơi nhạc.", 1990000m, 1490000m, 3, 6, 120, 4.5, "sale", "/images/products/p24-den.jpg"),
                ("Apple Watch Ultra 2", "apple-watch-ultra-2", "Vỏ Titan 49mm chuẩn hàng không vũ trụ, màn hình Retina sáng nhất 3000 nits, GPS băng tần kép L1 và L5 siêu chuẩn xác, đo độ sâu dưới nước 40m, pin dùng 72 giờ ở chế độ tiết kiệm.", 23990000m, 21490000m, 4, 1, 18, 4.9, "hot", "/images/products/p25-tu-nhien.jpg"),
                ("Samsung Galaxy Watch 7", "samsung-galaxy-watch-7", "Vi xử lý 3nm đầu tiên trên smartwatch, cảm biến BioActive tiên tiến theo dõi nhịp tim và giấc ngủ AI, thiết kế mặt tròn thanh lịch, theo dõi chỉ số sức khỏe chuyển hóa AGEs.", 7990000m, 6490000m, 4, 2, 42, 4.6, "sale", "/images/products/p26-xanh-la.jpg"),
                ("iPad Air M2 11inch", "ipad-air-m2-11inch", "Sức mạnh bùng nổ từ chip Apple M2, màn hình Liquid Retina 11 inch dải màu P3 sống động, hỗ trợ Apple Pencil Pro và Magic Keyboard phục vụ học tập và sáng tạo không giới hạn.", 18990000m, 16990000m, 5, 1, 28, 4.8, "sale", "/images/products/p27-xam-khong-gian.jpg"),
                ("Xiaomi Pad 6S Pro", "xiaomi-pad-6s-pro", "Màn hình 3K tỷ lệ vàng 3:2 tần số quét 144Hz, trang bị Snapdragon 8 Gen 2 cực đỉnh, công nghệ sạc thần tốc 120W đầy pin chỉ trong 35 phút, 6 loa vòm Dolby Atmos.", 14990000m, 12490000m, 5, 5, 32, 4.6, "sale", "/images/products/p28-xam-khong-gian.jpg"),
                ("Anker MagSafe Charger 15W", "anker-magsafe-charger-15w", "Đế sạc không dây hít từ tính MagSafe chuẩn Qi2 công suất sạc 15W ổn định cho iPhone 12-16 series, tản nhiệt nhôm nguyên khối, kèm dây bọc dù chống đứt gãy 1.5m.", 690000m, 490000m, 6, 6, 180, 4.6, "sale", "/images/products/p29-trang.jpg"),
                ("Anker PowerBank 24000mAh", "anker-powerbank-24000mah", "Pin sạc dự phòng dung lượng khủng 24.000mAh, công nghệ sạc nhanh PD 3.1 công suất tối đa 140W sạc được cho cả MacBook Pro và iPhone cùng lúc, màn hình số thông minh báo thông số.", 1890000m, 1490000m, 6, 6, 95, 4.7, "sale", "/images/products/p30-den.jpg"),
                ("Kính cường lực iPhone 16", "kinh-cuong-luc-iphone-16", "Kính cường lực cao cấp độ cứng 9H chống va đập, phủ Nano mạ điện chống bám vân tay mồ hôi, viền đen vát 2.5D ôm khít máy kèm khung tự dán tiện lợi.", 199000m, 99000m, 6, 6, 600, 4.3, "sale", "/images/products/p32-trong-suot.jpg"),
            };

            foreach (var (name, slug, desc, price, salePrice, catId, brandId, stock, rating, badge, imgUrl) in products)
            {
                var existing = db.Products.Include(p => p.Images).FirstOrDefault(p => p.Slug == slug);
                if (existing == null)
                {
                    var newProduct = new Product
                    {
                        Name = name,
                        Slug = slug,
                        Description = desc,
                        Price = price,
                        SalePrice = salePrice,
                        CategoryId = catId,
                        BrandId = brandId,
                        StockQuantity = stock,
                        RatingAvg = rating,
                        Badge = badge,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        Images = new List<ProductImage>
                        {
                            new ProductImage { ImageUrl = imgUrl, IsPrimary = true, SortOrder = 0 }
                        }
                    };
                    db.Products.Add(newProduct);
                }
            }
            db.SaveChanges();

            // Gán mã sản phẩm chuẩn SP-xxx nếu chưa có
            var allProds = db.Products.ToList();
            bool codeChanged = false;
            foreach (var p in allProds)
            {
                if (string.IsNullOrWhiteSpace(p.ProductCode))
                {
                    p.ProductCode = $"SP-{p.Id:D3}";
                    codeChanged = true;
                }
            }
            if (codeChanged) db.SaveChanges();

            SeedSpecsAndHighlightsByCategory(db);
        }

        private static void SeedCategoryMetadata(AppDbContext db)
        {
            var meta = new Dictionary<int, (string code, string desc, string[] specs)>
            {
                [1] = ("CAT-PHONE", "Điện thoại thông minh chính hãng Apple, Samsung, Xiaomi với hiệu năng và camera thế hệ mới.",
                    new[] { "Màn hình", "Bộ vi xử lý", "RAM", "Bộ nhớ trong", "Camera", "Dung lượng pin", "Hệ điều hành", "Kết nối" }),
                [2] = ("CAT-LAPTOP", "Máy tính xách tay văn phòng, đồ họa và laptop gaming hiệu năng cao.",
                    new[] { "CPU", "RAM", "Ổ cứng", "Card đồ họa", "Kích thước màn hình", "Độ phân giải", "Hệ điều hành", "Dung lượng pin" }),
                [3] = ("CAT-AUDIO", "Tai nghe không dây True Wireless, tai nghe chụp tai chống ồn ANC và thiết bị âm thanh Hi-Res.",
                    new[] { "Loại tai nghe", "Kết nối", "Thời lượng pin", "Công nghệ chống ồn", "Chuẩn chống nước", "Micro", "Cổng sạc" }),
                [4] = ("CAT-WATCH", "Đồng hồ thông minh theo dõi sức khỏe, thể thao chuyên nghiệp và định vị GPS.",
                    new[] { "Kích thước màn hình", "Công nghệ màn hình", "Thời lượng pin", "Cảm biến", "Khả năng chống nước", "Kết nối", "Hệ điều hành tương thích" }),
                [5] = ("CAT-TABLET", "Máy tính bảng phục vụ học tập, giải trí và làm việc sáng tạo chuyên nghiệp.",
                    new[] { "Màn hình", "Bộ vi xử lý", "RAM", "Bộ nhớ trong", "Camera", "Dung lượng pin", "Hệ điều hành", "Kết nối" }),
                [6] = ("CAT-ACCESSORY", "Phụ kiện công nghệ chính hãng: cáp sạc, đế sạc MagSafe, pin dự phòng, ốp lưng và kính cường lực.",
                    new[] { "Loại phụ kiện", "Công suất / Dung lượng", "Chuẩn kết nối", "Thiết bị tương thích", "Chất liệu / Kích thước" })
            };

            var categories = db.Categories.ToList();
            bool updated = false;
            foreach (var c in categories)
            {
                if (meta.TryGetValue(c.Id, out var m))
                {
                    if (string.IsNullOrWhiteSpace(c.Code)) { c.Code = m.code; updated = true; }
                    if (string.IsNullOrWhiteSpace(c.Description)) { c.Description = m.desc; updated = true; }
                    if (string.IsNullOrWhiteSpace(c.SpecTemplatesJson)) { c.SpecTemplatesJson = JsonSerializer.Serialize(m.specs); updated = true; }
                }
            }
            if (updated) db.SaveChanges();
        }

        private static void SeedSpecsAndHighlightsByCategory(AppDbContext db)
        {
            // Nếu hệ thống đã được khởi tạo đầy đủ Highlights cho tất cả sản phẩm thì không ghi đè chỉnh sửa của Admin
            if (db.ProductHighlights.Count() >= 80) return;

            var catalogData = new Dictionary<int, ((string k, string v)[] specs, string[] highlights)>
            {
                // 1. ĐIỆN THOẠI (CategoryId = 1) [1] = (
                    new[] {
                        ("Màn hình", "6.7\" Super Retina XDR OLED, ProMotion 120Hz, 2000 nits"),
                        ("Bộ vi xử lý", "Apple A17 Pro 6 nhân (tiến trình 3nm)"),
                        ("RAM", "8 GB"),
                        ("Bộ nhớ trong", "256 GB / 512 GB / 1 TB"),
                        ("Camera", "Chính 48MP OIS + Tele 5x 12MP + Góc siêu rộng 12MP | Trước 12MP"),
                        ("Dung lượng pin", "4422 mAh, sạc nhanh 20W USB-C & MagSafe 15W"),
                        ("Hệ điều hành", "iOS 17 (nâng cấp iOS 18)"),
                        ("Kết nối", "5G, Wi-Fi 6E, Bluetooth 5.3, NFC, USB-C 3.0")
                    },
                    new[] {
                        "Khung viền Titanium cấp 5 chuẩn hàng không vũ trụ siêu bền nhẹ kết hợp nút Action Button đa năng.",
                        "Vi xử lý Apple A17 Pro 3nm đầu tiên hỗ trợ Ray Tracing phần cứng, chiến mượt mọi tựa game AAA.",
                        "Hệ thống camera Pro 48MP kèm ống kính Telephoto tiềm vọng zoom quang học 5x (tiêu cự 120mm).",
                        "Cổng USB-C hỗ trợ chuẩn truyền dữ liệu USB 3 tốc độ lên đến 10Gbps và quay video ProRes 4K 60fps."
                    }
                ),
                [2] = (
                    new[] {
                        ("Màn hình", "6.8\" Dynamic AMOLED 2X, QHD+, 1-120Hz, 2600 nits, kính Gorilla Armor"),
                        ("Bộ vi xử lý", "Snapdragon 8 Gen 3 for Galaxy (8 nhân)"),
                        ("RAM", "12 GB"),
                        ("Bộ nhớ trong", "256 GB / 512 GB / 1 TB"),
                        ("Camera", "Chính 200MP OIS + Tele 5x 50MP + Tele 3x 10MP + Siêu rộng 12MP"),
                        ("Dung lượng pin", "5000 mAh, sạc siêu nhanh 45W"),
                        ("Hệ điều hành", "Android 14, One UI 6.1 (tích hợp Galaxy AI)"),
                        ("Kết nối", "5G, Wi-Fi 7, Bluetooth 5.3, UWB, USB-C 3.2")
                    },
                    new[] {
                        "Khung viền Titan bền bỉ cùng màn hình phẳng phủ kính Corning Gorilla Armor giảm 75% phản xạ ánh sáng.",
                        "Tích hợp sẵn bút cảm ứng S-Pen độ trễ cực thấp cùng bộ công cụ trí tuệ nhân tạo Galaxy AI toàn diện.",
                        "Hệ thống Quad Telephoto với cảm biến chính 200MP và camera tiềm vọng 50MP zoom quang 5x sắc nét.",
                        "Hiệu năng đỉnh cao từ Snapdragon 8 Gen 3 for Galaxy kèm buồng hơi tản nhiệt lớn hơn 1.9 lần."
                    }
                ),
                [12] = (
                    new[] {
                        ("Màn hình", "6.73\" LTPO AMOLED 2K+ (3200 x 1440), 1-120Hz, 3000 nits"),
                        ("Bộ vi xử lý", "Qualcomm Snapdragon 8 Gen 3 (4nm)"),
                        ("RAM", "16 GB LPDDR5X"),
                        ("Bộ nhớ trong", "256 GB / 512 GB UFS 4.0"),
                        ("Camera", "4 camera 50MP ống kính Leica Summilux (Cảm biến chính 1 inch LYT-900)"),
                        ("Dung lượng pin", "5000 mAh, sạc nhanh 90W có dây & 80W không dây"),
                        ("Hệ điều hành", "Xiaomi HyperOS (Android 14)"),
                        ("Kết nối", "5G, Wi-Fi 7, Bluetooth 5.4, NFC, Hồng ngoại, USB-C 3.2 Gen 2")
                    },
                    new[] {
                        "Hệ thống 4 camera đồng độ phân giải 50MP hợp tác chế tác cùng Leica với cảm biến 1 inch Sony LYT-900.",
                        "Khẩu độ biến thiên vô cấp f/1.63 - f/4.0 mang lại khả năng thu sáng và xóa phông quang học chuẩn máy ảnh.",
                        "Màn hình All Around Liquid Display 2K+ độ sáng 3000 nits bảo vệ bởi kính Xiaomi Shield Glass.",
                        "Sạc siêu tốc 90W HyperCharge nạp đầy 100% pin chỉ trong 33 phút."
                    }
                ),
                [13] = (
                    new[] {
                        ("Màn hình", "6.9\" Super Retina XDR OLED, ProMotion 1-120Hz, viền siêu mỏng"),
                        ("Bộ vi xử lý", "Apple A18 Pro 6 nhân (Neural Engine 16 lõi)"),
                        ("RAM", "8 GB"),
                        ("Bộ nhớ trong", "256 GB / 512 GB / 1 TB"),
                        ("Camera", "Chính 48MP Fusion + Siêu rộng 48MP + Tele 5x 12MP | Quay 4K 120fps Dolby Vision"),
                        ("Dung lượng pin", "4685 mAh (Xem video 33 giờ), sạc nhanh MagSafe 25W"),
                        ("Hệ điều hành", "iOS 18"),
                        ("Kết nối", "5G, Wi-Fi 7, Bluetooth 5.3, NFC, USB-C 3.2 Gen 2")
                    },
                    new[] {
                        "Nút điều khiển Camera Control cảm ứng lực thông minh giúp lấy nét, zoom và chụp ảnh tức thì.",
                        "Màn hình lớn nhất lịch sử iPhone 6.9 inch với viền bezel mỏng kỷ lục cùng kính Ceramic Shield thế hệ mới.",
                        "Chip A18 Pro tiến trình 3nm thế hệ 2 tăng tốc xử lý đồ họa và tối ưu cho Apple Intelligence.",
                        "Cặp camera 48MP Fusion & 48MP Ultra Wide hỗ trợ quay video 4K 120fps chuẩn điện ảnh Dolby Vision."
                    }
                ),
                [14] = (
                    new[] {
                        ("Màn hình", "6.9\" Dynamic AMOLED 2X QHD+, 1-120Hz, Gorilla Armor 2 chống chói"),
                        ("Bộ vi xử lý", "Snapdragon 8 Elite for Galaxy (tiến trình 3nm)"),
                        ("RAM", "12 GB"),
                        ("Bộ nhớ trong", "256 GB / 512 GB / 1 TB"),
                        ("Camera", "Chính 200MP OIS + Siêu rộng 50MP + Tele 5x 50MP + Tele 3x 10MP"),
                        ("Dung lượng pin", "5000 mAh, sạc nhanh 45W & Sạc không dây Qi2"),
                        ("Hệ điều hành", "Android 15, One UI 7 với Galaxy AI thế hệ mới"),
                        ("Kết nối", "5G, Wi-Fi 7, Bluetooth 5.4, UWB, USB-C 3.2")
                    },
                    new[] {
                        "Sức mạnh vượt trội từ vi xử lý Snapdragon 8 Elite for Galaxy 3nm tăng 40% hiệu năng NPU AI.",
                        "Nâng cấp camera góc siêu rộng lên 50MP kết hợp cảm biến chính 200MP và ProVisual Engine thế hệ mới.",
                        "Thiết kế bo góc tinh tế với khung Titan siêu bền và màn hình 6.9 inch viền mỏng ấn tượng.",
                        "Trợ lý AI đa phương thức trên One UI 7 hỗ trợ dịch thuật trực tiếp, tóm tắt công việc và hậu kỳ ảnh chuyên sâu."
                    }
                ),
                [15] = (
                    new[] {
                        ("Màn hình", "6.73\" LTPO AMOLED 2K (3200 x 1440), 1-120Hz, độ sáng 3200 nits"),
                        ("Bộ vi xử lý", "Qualcomm Snapdragon 8 Elite (3nm)"),
                        ("RAM", "12 GB / 16 GB LPDDR5X"),
                        ("Bộ nhớ trong", "256 GB / 512 GB UFS 4.0"),
                        ("Camera", "Bộ 3 camera 50MP ống kính Leica Summilux (Chính Light Fusion 900 + Tele 5x + Siêu rộng)"),
                        ("Dung lượng pin", "6100 mAh Silicon-Carbon, sạc nhanh 90W có dây & 50W không dây"),
                        ("Hệ điều hành", "Xiaomi HyperOS 2.0 (Android 15)"),
                        ("Kết nối", "5G, Wi-Fi 7, Bluetooth 5.4, Cảm biến vân tay siêu âm")
                    },
                    new[] {
                        "Viên pin Silicon-Carbon dung lượng khủng 6100 mAh trong thân máy mỏng gọn chỉ 8.35mm.",
                        "Vi xử lý Snapdragon 8 Elite 3nm mới nhất kết hợp hệ thống tản nhiệt cánh ngầm Xiaomi Wing-type.",
                        "Cụm 3 ống kính Leica Summilux 50MP với camera tiềm vọng zoom quang 5x và chụp telemacro 30cm.",
                        "Màn hình M9 LTPO 2K siêu sáng 3200 nits tích hợp cảm biến vân tay siêu âm mở khóa siêu nhạy."
                    }
                ),
                [16] = (
                    new[] {
                        ("Màn hình", "6.6\" Super AMOLED FHD+, 120Hz, Vision Booster 1000 nits"),
                        ("Bộ vi xử lý", "Exynos 1480 8 nhân (4nm) kèm GPU AMD Xclipse 530"),
                        ("RAM", "8 GB"),
                        ("Bộ nhớ trong", "128 GB / 256 GB (hỗ trợ thẻ nhớ MicroSD đến 1TB)"),
                        ("Camera", "Chính 50MP OIS + Siêu rộng 12MP + Macro 5MP | Trước 32MP"),
                        ("Dung lượng pin", "5000 mAh, sạc nhanh 25W"),
                        ("Hệ điều hành", "Android 14, One UI 6.1, bảo mật Samsung Knox Vault"),
                        ("Kết nối", "5G, Wi-Fi 6, Bluetooth 5.3, NFC, Kháng nước IP67")
                    },
                    new[] {
                        "Hoàn thiện sang trọng với khung viền kim loại vát phẳng và mặt lưng kính Gorilla Glass Victus+.",
                        "Camera chính 50MP tích hợp chống rung quang học OIS và chế độ chụp đêm Nightography sắc nét.",
                        "Chuẩn kháng nước và kháng bụi IP67 giúp an tâm sử dụng trong mọi điều kiện thời tiết.",
                        "Bảo mật cấp độ phần cứng Samsung Knox Vault cùng cam kết 4 thế hệ cập nhật hệ điều hành."
                    }
                ),
                [17] = (
                    new[] {
                        ("Màn hình", "6.1\" Super Retina XDR OLED, Dynamic Island, 2000 nits"),
                        ("Bộ vi xử lý", "Apple A16 Bionic 6 nhân"),
                        ("RAM", "6 GB"),
                        ("Bộ nhớ trong", "128 GB / 256 GB / 512 GB"),
                        ("Camera", "Chính 48MP (Zoom 2x quang học) + Siêu rộng 12MP | Trước 12MP TrueDepth"),
                        ("Dung lượng pin", "3349 mAh (Xem video 20 giờ), sạc nhanh 20W USB-C & MagSafe"),
                        ("Hệ điều hành", "iOS 17 (nâng cấp iOS 18)"),
                        ("Kết nối", "5G, Wi-Fi 6, Bluetooth 5.3, NFC, Cổng USB-C")
                    },
                    new[] {
                        "Thiết kế mặt lưng kính pha màu nhám sang trọng cùng khu vực tương tác thông minh Dynamic Island.",
                        "Camera chính nâng cấp lên 48MP hỗ trợ chụp ảnh chân dung thế hệ mới và Telephoto 2x chất lượng quang học.",
                        "Trang bị chip A16 Bionic mạnh mẽ cùng cổng kết nối USB-C tiện lợi dùng chung cáp sạc với Mac và iPad.",
                        "Khung nhôm chuẩn hàng không vũ trụ kết hợp mặt kính Ceramic Shield bền bỉ, kháng nước IP68."
                    }
                ),

                // 2. LAPTOP (CategoryId = 2) [3] = (
                    new[] {
                        ("CPU", "Apple M3 (8 nhân CPU, 10 nhân GPU, 16 nhân Neural Engine)"),
                        ("RAM", "16 GB / 24 GB Unified Memory"),
                        ("Ổ cứng", "256 GB / 512 GB SSD PCIe"),
                        ("Card đồ họa", "GPU tích hợp 10 nhân Apple M3 (hỗ trợ Hardware Ray Tracing)"),
                        ("Kích thước màn hình", "13.6 inch Liquid Retina IPS, True Tone, 500 nits"),
                        ("Độ phân giải", "2560 x 1664 pixels (Dải màu rộng P3)"),
                        ("Hệ điều hành", "macOS Sonoma / Sequoia"),
                        ("Dung lượng pin", "52.6 Wh (Thời lượng sử dụng lên đến 18 giờ), sạc MagSafe 3")
                    },
                    new[] {
                        "Thiết kế nhôm nguyên khối siêu mỏng 1.13 cm, trọng lượng chỉ 1.24 kg dễ dàng mang theo mọi nơi.",
                        "Chip Apple M3 tiến trình 3nm mang lại hiệu năng nhanh hơn đến 60% và hỗ trợ xuất 2 màn hình rời.",
                        "Màn hình Liquid Retina 13.6 inch hỗ trợ 1 tỷ màu, hệ thống 4 loa Spatial Audio và camera 1080p sắc nét.",
                        "Vận hành hoàn toàn êm ái không quạt tản nhiệt với thời lượng pin bền bỉ suốt 18 giờ làm việc."
                    }
                ),
                [7] = (
                    new[] {
                        ("CPU", "AMD Ryzen 9 8945HS (8 nhân 16 luồng, xung nhịp tối đa 5.2GHz, Ryzen AI)"),
                        ("RAM", "16 GB / 32 GB LPDDR5X 6400MHz"),
                        ("Ổ cứng", "1 TB PCIe 4.0 NVMe M.2 SSD"),
                        ("Card đồ họa", "NVIDIA GeForce RTX 4070 8GB GDDR6 (MUX Switch + NVIDIA Advanced Optimus)"),
                        ("Kích thước màn hình", "14 inch ROG Nebula Display OLED 165Hz, 0.2ms, G-SYNC"),
                        ("Độ phân giải", "2.8K (2880 x 1800) tỷ lệ 16:10, 100% DCI-P3, chuẩn Pantone Validated"),
                        ("Hệ điều hành", "Windows 11 Home bản quyền"),
                        ("Dung lượng pin", "73 Wh, sạc nhanh 180W và USB-C PD 100W")
                    },
                    new[] {
                        "Vỏ nhôm CNC nguyên khối siêu gọn nhẹ chỉ 1.5 kg với dải đèn Slash Lighting LED độc quyền mặt A.",
                        "Màn hình ROG Nebula OLED 2.8K 165Hz đầu tiên trên dòng Zephyrus với độ tương phản 1.000.000:1.",
                        "Hiệu năng gaming và đồ họa khủng từ bộ đôi AMD Ryzen 9 8945HS và GPU NVIDIA RTX 4070.",
                        "Hệ thống tản nhiệt ROG Intelligent Cooling 3 quạt kèm keo tản nhiệt kim loại lỏng Liquid Metal."
                    }
                ),
                [18] = (
                    new[] {
                        ("CPU", "Apple M4 Pro (12 nhân CPU, 16 nhân GPU, 16 nhân Neural Engine)"),
                        ("RAM", "24 GB / 48 GB Unified Memory (băng thông 273GB/s)"),
                        ("Ổ cứng", "512 GB / 1 TB SSD tốc độ cao"),
                        ("Card đồ họa", "GPU tích hợp 16 nhân Apple M4 Pro"),
                        ("Kích thước màn hình", "14.2 inch Liquid Retina XDR Mini-LED, ProMotion 120Hz, 1600 nits"),
                        ("Độ phân giải", "3024 x 1964 pixels (254 ppi, 1 tỷ màu)"),
                        ("Hệ điều hành", "macOS Sequoia"),
                        ("Dung lượng pin", "72.4 Wh (Thời lượng pin lên đến 24 giờ), sạc nhanh MagSafe 3")
                    },
                    new[] {
                        "Sức mạnh đột phá từ chip Apple M4 Pro xử lý mượt mà các dự án lập trình, 3D và dựng phim 8K.",
                        "Trang bị 3 cổng Thunderbolt 5 thế hệ mới với tốc độ truyền dữ liệu siêu tốc lên tới 120Gb/s.",
                        "Màn hình Liquid Retina XDR 120Hz độ sáng đỉnh 1600 nits cùng camera 12MP Center Stage Desk View.",
                        "Thời lượng pin kỷ lục lên đến 24 giờ sử dụng liên tục chỉ với một lần sạc."
                    }
                ),
                [33] = (
                    new[] {
                        ("CPU", "Apple M4 Pro (12 nhân CPU, 16 nhân GPU, 16 nhân Neural Engine)"),
                        ("RAM", "24 GB / 48 GB Unified Memory (băng thông 273GB/s)"),
                        ("Ổ cứng", "512 GB / 1 TB SSD tốc độ cao"),
                        ("Card đồ họa", "GPU tích hợp 16 nhân Apple M4 Pro"),
                        ("Kích thước màn hình", "14.2 inch Liquid Retina XDR Mini-LED, ProMotion 120Hz, 1600 nits"),
                        ("Độ phân giải", "3024 x 1964 pixels (254 ppi, 1 tỷ màu)"),
                        ("Hệ điều hành", "macOS Sequoia"),
                        ("Dung lượng pin", "72.4 Wh (Thời lượng pin lên đến 24 giờ), sạc nhanh MagSafe 3")
                    },
                    new[] {
                        "Sức mạnh đột phá từ chip Apple M4 Pro xử lý mượt mà các dự án lập trình, 3D và dựng phim 8K.",
                        "Trang bị 3 cổng Thunderbolt 5 thế hệ mới với tốc độ truyền dữ liệu siêu tốc lên tới 120Gb/s.",
                        "Màn hình Liquid Retina XDR 120Hz độ sáng đỉnh 1600 nits cùng camera 12MP Center Stage Desk View.",
                        "Thời lượng pin kỷ lục lên đến 24 giờ sử dụng liên tục chỉ với một lần sạc."
                    }
                ),
                [19] = (
                    new[] {
                        ("CPU", "Intel Core i5-13500H (12 nhân 16 luồng, xung nhịp tối đa 4.7GHz)"),
                        ("RAM", "16 GB DDR4 3200MHz"),
                        ("Ổ cứng", "512 GB / 1 TB M.2 NVMe PCIe 4.0 SSD"),
                        ("Card đồ họa", "Intel Iris Xe Graphics"),
                        ("Kích thước màn hình", "15.6 inch Lumina OLED, 120Hz, 0.2ms, 600 nits HDR"),
                        ("Độ phân giải", "2.8K (2880 x 1620), 100% DCI-P3, giảm 70% ánh sáng xanh TÜV Rheinland"),
                        ("Hệ điều hành", "Windows 11 Home bản quyền"),
                        ("Dung lượng pin", "50 Wh, hỗ trợ sạc nhanh 65W (60% trong 49 phút)")
                    },
                    new[] {
                        "Màn hình ASUS Lumina OLED 2.8K 120Hz rực rỡ chuẩn màu điện ảnh 100% DCI-P3 bảo vệ mắt.",
                        "Vi xử lý Intel Core i5 dòng H hiệu năng cao 12 nhân đáp ứng mượt mà cả công việc văn phòng lẫn đồ họa 2D.",
                        "Bản lề mở phẳng 180 độ linh hoạt, tích hợp cảm biến vân tay một chạm và nắp che webcam vật lý.",
                        "Lớp phủ kháng khuẩn ASUS Antimicrobial Guard Plus bảo vệ bề mặt máy khỏi 99% vi khuẩn."
                    }
                ),
                [20] = (
                    new[] {
                        ("CPU", "Intel Core Ultra 7 155H (16 nhân 22 luồng, tích hợp NPU Intel AI Boost)"),
                        ("RAM", "16 GB / 32 GB LPDDR5x 6400MHz"),
                        ("Ổ cứng", "512 GB / 1 TB PCIe Gen 4 NVMe Performance SSD"),
                        ("Card đồ họa", "Intel Arc Graphics tích hợp"),
                        ("Kích thước màn hình", "14 inch OLED chống chói, 120Hz, 400 nits, Dolby Vision"),
                        ("Độ phân giải", "2.8K (2880 x 1800) tỷ lệ 16:10, 100% DCI-P3"),
                        ("Hệ điều hành", "Windows 11 Pro bản quyền"),
                        ("Dung lượng pin", "57 Wh (Thời lượng lên đến 15 giờ), sạc nhanh Rapid Charge 65W USB-C")
                    },
                    new[] {
                        "Khung vỏ sợi Carbon hàng không siêu bền đạt chuẩn quân đội MIL-STD-810H với trọng lượng nhẹ chỉ 1.12 kg.",
                        "Vi xử lý Intel Core Ultra 7 tích hợp NPU AI chuyên dụng tăng tốc các tác vụ họp trực tuyến và xử lý thông minh.",
                        "Bàn phím ThinkPad TrackPoint trứ danh chống tràn nước với hành trình phím êm ái và Haptic Touchpad.",
                        "Bảo mật doanh nghiệp toàn diện với chip dTPM 2.0, camera hồng ngoại IR nhận diện khuôn mặt và cảm biến vân tay."
                    }
                ),
                [21] = (
                    new[] {
                        ("CPU", "AMD Ryzen 7 7745HX (8 nhân 16 luồng, xung nhịp tối đa 5.1GHz)"),
                        ("RAM", "16 GB / 32 GB DDR5 4800MHz (2 khe nâng cấp tối đa 64GB)"),
                        ("Ổ cứng", "512 GB / 1 TB PCIe 4.0 NVMe M.2 SSD (2 khe M.2)"),
                        ("Card đồ họa", "NVIDIA GeForce RTX 4060 8GB GDDR6 TGP tối đa 140W (MUX Switch)"),
                        ("Kích thước màn hình", "15.6 inch IPS chống chói, tần số quét 144Hz, G-SYNC"),
                        ("Độ phân giải", "Full HD (1920 x 1080), chuẩn màu 100% sRGB"),
                        ("Hệ điều hành", "Windows 11 Home bản quyền"),
                        ("Dung lượng pin", "90 Wh dung lượng lớn, hỗ trợ sạc nhanh USB-C 100W")
                    },
                    new[] {
                        "Hiệu năng chiến game mạnh mẽ với GPU RTX 4060 TGP full 140W tích hợp MUX Switch và NVIDIA Advanced Optimus.",
                        "Độ bền chuẩn quân đội Mỹ MIL-STD-810H chống va đập, rung lắc và nhiệt độ khắc nghiệt.",
                        "Hệ thống tản nhiệt Arc Flow 84 cánh quạt kết hợp 5 ống đồng giúp máy luôn mát mẻ khi tải nặng.",
                        "Viên pin lớn 90Wh hiếm có trên laptop gaming cùng bàn phím RGB Mini-LED nổi bật cụm phím WASD."
                    }
                ),

                // 3. TAI NGHE / ÂM THANH (CategoryId = 3) [4] = (
                    new[] {
                        ("Loại tai nghe", "Tai nghe chụp tai không dây chống ồn (Over-ear Wireless)"),
                        ("Kết nối", "Bluetooth 5.2 (Multipoint kết nối 2 thiết bị), Jack 3.5mm"),
                        ("Thời lượng pin", "Lên đến 30 giờ (bật ANC) / 40 giờ (tắt ANC), sạc nhanh 3 phút nghe 3 giờ"),
                        ("Công nghệ chống ồn", "Auto NC Optimizer với 2 bộ xử lý QN1 + V1 và 8 micro chống ồn"),
                        ("Chuẩn chống nước", "Không hỗ trợ (Thiết kế dùng trong nhà / văn phòng / di chuyển)"),
                        ("Micro", "4 micro форми chùm tia (Beamforming) tích hợp AI lọc gió Precise Voice Pickup"),
                        ("Cổng sạc", "USB Type-C (Hỗ trợ sạc nhanh USB-PD)")
                    },
                    new[] {
                        "Khả năng chống ồn chủ động ANC dẫn đầu thị trường nhờ kết hợp bộ xử lý HD QN1, chip V1 và 8 micro thu âm.",
                        "Màng loa 30mm sợi carbon thiết kế mới tái tạo âm thanh Hi-Res Audio Wireless chuẩn LDAC và DSEE Extreme.",
                        "Đệm tai bằng da tổng hợp Soft Fit siêu êm ái, trọng lượng nhẹ chỉ 250g đeo thoải mái suốt ngày dài.",
                        "Tính năng Speak-to-Chat tự động dừng nhạc khi bạn cất tiếng nói và kết nối đa điểm 2 thiết bị cùng lúc."
                    }
                ),
                [8] = (
                    new[] {
                        ("Loại tai nghe", "Tai nghe Bluetooth True Wireless In-ear"),
                        ("Kết nối", "Bluetooth 5.3, Chip Apple H2 & Chip U1 trong hộp sạc"),
                        ("Thời lượng pin", "Tai nghe 6 giờ + Hộp sạc 30 giờ (bật chống ồn ANC)"),
                        ("Công nghệ chống ồn", "Chống ồn chủ động ANC gấp 2 lần, Âm thanh thích ứng Adaptive Audio, Xuyên âm"),
                        ("Chuẩn chống nước", "Kháng nước và bụi chuẩn IP54 (cho cả tai nghe và hộp sạc)"),
                        ("Micro", "Micro hướng vào trong kết hợp thuật toán Cách ly giọng nói (Voice Isolation)"),
                        ("Cổng sạc", "USB Type-C, Sạc không dây MagSafe, Qi và bộ sạc Apple Watch")
                    },
                    new[] {
                        "Trang bị chip Apple H2 khử tiếng ồn chủ động gấp 2 lần thế hệ trước cùng chế độ Âm thanh thích ứng thông minh.",
                        "Tính năng Nhận biết cuộc hội thoại tự động giảm âm lượng nhạc và làm rõ giọng người đối diện khi bạn giao tiếp.",
                        "Âm thanh không gian cá nhân hóa (Personalized Spatial Audio) theo dõi chuyển động đầu sống động.",
                        "Hộp sạc MagSafe chuẩn USB-C tích hợp loa báo âm thanh tìm kiếm chính xác qua ứng dụng Find My."
                    }
                ),
                [22] = (
                    new[] {
                        ("Loại tai nghe", "Tai nghe Bluetooth True Wireless In-ear Hi-Res"),
                        ("Kết nối", "Bluetooth 5.3 (Hỗ trợ LDAC, AAC, SBC, LC3 LE Audio, Multipoint)"),
                        ("Thời lượng pin", "Tai nghe 8 giờ (bật ANC) / 12 giờ (tắt ANC) + Hộp sạc 24 giờ"),
                        ("Công nghệ chống ồn", "Bộ xử lý chống ồn HD QN2e + Chip V2 kép và 6 micro cảm biến tiếng ồn"),
                        ("Chuẩn chống nước", "Kháng nước và mồ hôi chuẩn IPX4"),
                        ("Micro", "Cảm biến dẫn truyền xương (Bone Conduction) + AI giảm tiếng ồn môi trường"),
                        ("Cổng sạc", "USB Type-C & Sạc không dây chuẩn Qi")
                    },
                    new[] {
                        "Thiết kế nhỏ gọn hơn 25% và nhẹ hơn 20% thế hệ trước với nút tai bọt biển cách âm Noise Isolation Earbud Tips.",
                        "Sức mạnh khử ồn đỉnh cao từ cặp vi xử lý độc quyền Sony QN2e và Integrated Processor V2.",
                        "Màng loa Dynamic Driver X 8.4mm tái tạo dải trầm sâu lắng và giọng hát chi tiết chuẩn Hi-Res Audio Wireless.",
                        "Đàm thoại trong trẻo ngay cả nơi ồn ào nhờ cảm biến truyền âm qua xương và thuật toán AI lọc gió."
                    }
                ),
                [23] = (
                    new[] {
                        ("Loại tai nghe", "Tai nghe Bluetooth True Wireless In-ear (Thiết kế Blade công thái học)"),
                        ("Kết nối", "Bluetooth 5.4 (Samsung Seamless Codec 24-bit/96kHz, AAC, SBC, Auto Switch)"),
                        ("Thời lượng pin", "Tai nghe 6 giờ (bật ANC) / 7 giờ (tắt ANC) + Hộp sạc lên đến 26 - 30 giờ"),
                        ("Công nghệ chống ồn", "ANC thích ứng Galaxy AI, Phát hiện còi báo động & Nhận diện giọng nói"),
                        ("Chuẩn chống nước", "Kháng nước và kháng bụi chuẩn IP57 (ngâm nước sâu 1m trong 30 phút)"),
                        ("Micro", "Hệ thống 3 micro SNR cao mỗi bên + VPU thu giọng nói qua xương hàm"),
                        ("Cổng sạc", "USB Type-C & Sạc không dây chuẩn Qi")
                    },
                    new[] {
                        "Hệ thống loa 2 chiều nâng cấp (10.5mm Dynamic + 6.1mm Planar) cùng bộ khuếch đại kép xuất âm thanh Hi-Fi 24-bit/96kHz.",
                        "Thiết kế đuôi tai nghe góc cạnh tích hợp dải đèn LED Blade Lights hiện đại, điều khiển vuốt và bóp trực quan.",
                        "Công nghệ Galaxy AI tự động phân tích tiếng ồn môi trường để điều chỉnh mức chống ồn ANC và âm xuyên âm tối ưu.",
                        "Hỗ trợ phiên dịch viên trực tiếp theo thời gian thực khi kết hợp cùng điện thoại Galaxy và chuẩn kháng nước bụi IP57."
                    }
                ),
                [24] = (
                    new[] {
                        ("Loại tai nghe", "Tai nghe chụp tai không dây chống ồn (Over-ear Wireless)"),
                        ("Kết nối", "Bluetooth 5.3 (Hỗ trợ codec LDAC Hi-Res, AAC, SBC) & Cáp AUX 3.5mm"),
                        ("Thời lượng pin", "Lên đến 50 giờ (bật ANC) / 65 giờ (tắt ANC), sạc nhanh 5 phút nghe 4 giờ"),
                        ("Công nghệ chống ồn", "Adaptive ANC 3 cấp độ giảm tới 98% tiếng ồn tần số thấp"),
                        ("Chuẩn chống nước", "Không hỗ trợ"),
                        ("Micro", "2 micro tích hợp thuật toán AI tăng cường giọng nói khi đàm thoại"),
                        ("Cổng sạc", "USB Type-C")
                    },
                    new[] {
                        "Hệ thống khử tiếng ồn 3 giai đoạn loại bỏ đến 98% tiếng ồn động cơ máy bay, tàu xe và văn phòng.",
                        "Đạt chứng nhận kép Hi-Res Audio và Hi-Res Audio Wireless với màng loa 40mm hai lớp màng lụa và gốm.",
                        "Thời lượng pin siêu khủng lên đến 50 giờ khi bật chống ồn và 65 giờ ở chế độ tiêu chuẩn.",
                        "Tùy chỉnh EQ 22 chế độ chuyên sâu và chế độ Xuyên âm dễ dàng qua ứng dụng Soundcore."
                    }
                ),

                // 4. ĐỒNG HỒ THÔNG MINH (CategoryId = 4) [5] = (
                    new[] {
                        ("Kích thước màn hình", "41mm / 45mm (Viền mỏng, diện tích hiển thị lớn)"),
                        ("Công nghệ màn hình", "Always-On Retina LTPO OLED, độ sáng tối đa 2000 nits, mặt kính Ion-X / Sapphire"),
                        ("Thời lượng pin", "Lên đến 18 giờ sử dụng tiêu chuẩn / 36 giờ ở chế độ Nguồn điện thấp"),
                        ("Cảm biến", "Đo điện tâm đồ ECG, Nhịp tim quang học thế hệ 3, Oxy trong máu SpO2, Cảm biến nhiệt độ"),
                        ("Khả năng chống nước", "Chống nước 50m (WR50), kháng bụi chuẩn IP6X"),
                        ("Kết nối", "GPS, Wi-Fi 4, Bluetooth 5.3, NFC (Apple Pay), Chip Ultra Wideband thế hệ 2"),
                        ("Hệ điều hành tương thích", "watchOS 10/11 (Tương thích iPhone chạy iOS 17 trở lên)")
                    },
                    new[] {
                        "Trang bị vi xử lý S9 SiP mạnh mẽ cho phép thao tác chạm hai ngón tay (Double Tap) không cần chạm màn hình.",
                        "Màn hình Always-On Retina tăng độ sáng gấp đôi lên tới 2000 nits nhìn rõ dưới nắng gắt và hạ tối thiểu 1 nit trong phòng tối.",
                        "Theo dõi sức khỏe chuyên sâu: Đo điện tâm đồ ECG, nồng độ oxy trong máu SpO2, theo dõi giấc ngủ và phát hiện va chạm.",
                        "Tích hợp chip Ultra Wideband thế hệ 2 giúp định vị chính xác vị trí iPhone của bạn đến từng mét."
                    }
                ),
                [10] = (
                    new[] {
                        ("Kích thước màn hình", "46mm (Mặt tròn 1.43 inch, độ phân giải 466 x 466 pixels)"),
                        ("Công nghệ màn hình", "AMOLED sắc nét, 326 ppi, độ sáng 600 nits, hỗ trợ Always-On Display"),
                        ("Thời lượng pin", "495 mAh (Lên đến 65 giờ sử dụng thông minh), sạc từ tính siêu tốc 45 phút"),
                        ("Cảm biến", "Phân tích thành phần cơ thể BIA, Nhịp tim quang học, SpO2, Khí áp kế, La bàn"),
                        ("Khả năng chống nước", "5 ATM (50m - Phù hợp bơi lội vùng nước nông)"),
                        ("Kết nối", "GPS băng tần kép L1+L5, Wi-Fi băng tần kép, Bluetooth 5.2, NFC"),
                        ("Hệ điều hành tương thích", "Google Wear OS (Tương thích điện thoại Android 8.0 trở lên)")
                    },
                    new[] {
                        "Chạy hệ điều hành Google Wear OS mượt mà trên nền tảng chip Snapdragon W5+ Gen 1 tiến trình 4nm.",
                        "Khung vỏ thép không gỉ đánh bóng sang trọng với núm xoay cổ điển và hơn 150 chế độ luyện tập thể thao.",
                        "Đo lường thành phần cơ thể (tỷ lệ mỡ, cơ xương, lượng nước) chỉ với thao tác đặt hai ngón tay lên nút bấm.",
                        "Định vị độc lập chính xác cao nhờ hệ thống GNSS 5 vệ tinh băng tần kép L1 + L5."
                    }
                ),
                [25] = (
                    new[] {
                        ("Kích thước màn hình", "49mm (Mặt đồng hồ phẳng lớn nhất của Apple, vỏ Titanium)"),
                        ("Công nghệ màn hình", "Always-On Retina LTPO2 OLED, độ sáng kỷ lục 3000 nits, mặt kính Sapphire phẳng"),
                        ("Thời lượng pin", "Lên đến 36 giờ sử dụng bình thường / 72 giờ ở chế độ Nguồn điện thấp"),
                        ("Cảm biến", "Độ sâu nước & Nhiệt độ nước, ECG, SpO2, Nhịp tim, Cao độ kế luôn bật, La bàn"),
                        ("Khả năng chống nước", "Chống nước 100m (WR100), đạt chuẩn lặn giải trí EN13319 đến 40m, IP6X"),
                        ("Kết nối", "LTE/eSIM, GPS chính xác tần số kép (L1 & L5), Wi-Fi, Bluetooth 5.3, Còi báo động 86dB"),
                        ("Hệ điều hành tương thích", "watchOS 10/11 (Tương thích iPhone Xs trở lên chạy iOS 17+)")
                    },
                    new[] {
                        "Vỏ Titanium 49mm chuẩn hàng không vũ trụ siêu bền với gờ nổi bảo vệ mặt kính Sapphire khỏi va đập cạnh.",
                        "Màn hình sáng nhất từ trước đến nay của Apple đạt 3000 nits cùng chế độ Ban đêm tự động kích hoạt nhờ cảm biến ánh sáng.",
                        "Tích hợp nút Tác Vụ (Action Button) màu cam quốc tế tùy chỉnh nhanh và còi cứu hộ 86 decibel vang xa 180 mét.",
                        "Hệ thống GPS tần số kép L1 và L5 định vị siêu chính xác cho chạy bộ địa hình, leo núi và lặn biển sâu 40m."
                    }
                ),
                [26] = (
                    new[] {
                        ("Kích thước màn hình", "40mm (1.3 inch) / 44mm (1.5 inch)"),
                        ("Công nghệ màn hình", "Super AMOLED Always-On Display, độ sáng 2000 nits, mặt kính Sapphire Crystal"),
                        ("Thời lượng pin", "300 mAh (40mm) / 425 mAh (44mm) - Sử dụng lên đến 40 giờ, sạc nhanh không dây"),
                        ("Cảm biến", "Samsung BioActive thế hệ mới (13 đèn LED), ECG, Huyết áp, BIA, Chỉ số AGEs, Nhiệt độ da"),
                        ("Khả năng chống nước", "5 ATM + IP68, độ bền chuẩn quân đội MIL-STD-810H"),
                        ("Kết nối", "GPS băng tần kép L1+L5, Bluetooth 5.3, Wi-Fi, NFC (Tuỳ chọn LTE)"),
                        ("Hệ điều hành tương thích", "Wear OS Powered by Samsung (One UI 6 Watch - Android 11 trở lên)")
                    },
                    new[] {
                        "Smartwatch đầu tiên trang bị vi xử lý Exynos W1000 tiến trình 3nm 5 nhân nhanh gấp 3 lần thế hệ trước.",
                        "Cảm biến BioActive thế hệ mới tăng độ chính xác khi đo nhịp tim tập luyện cường độ cao và theo dõi chỉ số AGEs.",
                        "Tính năng Điểm năng lượng (Energy Score) và Huấn luyện viên giấc ngủ phân tích sức khỏe hàng ngày bằng Galaxy AI.",
                        "Nâng cấp hệ thống GPS băng tần kép L1 + L5 giúp theo dõi lộ trình chạy bộ và đạp xe chính xác ngay cả trong đô thị."
                    }
                ),

                // 5. MÁY TÍNH BẢNG (CategoryId = 5) [6] = (
                    new[] {
                        ("Màn hình", "11 inch Ultra Retina XDR (OLED 2 lớp Tandem), ProMotion 10-120Hz, 1600 nits HDR"),
                        ("Bộ vi xử lý", "Apple M4 (9 nhân CPU, 10 nhân GPU, 16 nhân Neural Engine)"),
                        ("RAM", "8 GB / 16 GB"),
                        ("Bộ nhớ trong", "256 GB / 512 GB / 1 TB"),
                        ("Camera", "Sau 12MP Wide + Cảm biến LiDAR | Trước 12MP Ultra Wide cạnh ngang Center Stage"),
                        ("Dung lượng pin", "31.29 Wh (Thời lượng sử dụng lên đến 10 giờ lướt web/xem video)"),
                        ("Hệ điều hành", "iPadOS 17 / iPadOS 18"),
                        ("Kết nối", "Wi-Fi 6E (hoặc 5G), Bluetooth 5.3, Cổng Thunderbolt / USB 4")
                    },
                    new[] {
                        "Thiết kế mỏng nhất lịch sử của Apple chỉ 5.3 mm nhưng sở hữu sức mạnh đột phá từ siêu chip Apple M4.",
                        "Màn hình Ultra Retina XDR công nghệ OLED hai lớp (Tandem OLED) mang lại độ tương phản 2.000.000:1 tuyệt mỹ.",
                        "Hỗ trợ bút Apple Pencil Pro với cảm biến bóp, xoay thân bút và bàn phím Magic Keyboard vỏ nhôm mới.",
                        "Camera trước 12MP chuyển sang cạnh ngang kết hợp Face ID và 4 micro chuẩn phòng thu tiện lợi khi họp trực tuyến."
                    }
                ),
                [9] = (
                    new[] {
                        ("Màn hình", "12.4 inch Dynamic AMOLED 2X, 120Hz, HDR10+, Vision Booster"),
                        ("Bộ vi xử lý", "Qualcomm Snapdragon 8 Gen 2 for Galaxy (8 nhân)"),
                        ("RAM", "12 GB"),
                        ("Bộ nhớ trong", "256 GB / 512 GB (Hỗ trợ thẻ nhớ MicroSD đến 1TB)"),
                        ("Camera", "Sau kép: Chính 13MP + Siêu rộng 8MP | Trước: 12MP góc siêu rộng"),
                        ("Dung lượng pin", "10.090 mAh, hỗ trợ sạc siêu nhanh 45W"),
                        ("Hệ điều hành", "Android 14, One UI 6 tích hợp Samsung DeX & Galaxy AI"),
                        ("Kết nối", "Wi-Fi 6E / 5G, Bluetooth 5.3, USB Type-C 3.2, 4 loa AKG Dolby Atmos")
                    },
                    new[] {
                        "Máy tính bảng cao cấp hiếm hoi đạt chuẩn kháng nước và kháng bụi IP68 cho cả thân máy và bút S-Pen đi kèm.",
                        "Màn hình lớn 12.4 inch Dynamic AMOLED 2X 120Hz kết hợp hệ thống 4 loa tinh chỉnh bởi AKG chuẩn Dolby Atmos.",
                        "Chế độ Samsung DeX biến máy tính bảng thành giao diện làm việc đa cửa sổ như máy tính xách tay.",
                        "Viên pin khủng 10.090 mAh đáp ứng trọn ngày làm việc sáng tạo, ghi chú và giải trí."
                    }
                ),
                [27] = (
                    new[] {
                        ("Màn hình", "11 inch Liquid Retina IPS, True Tone, dải màu P3, lớp phủ chống phản chiếu"),
                        ("Bộ vi xử lý", "Apple M2 (8 nhân CPU, 9 nhân GPU, 16 nhân Neural Engine)"),
                        ("RAM", "8 GB Unified Memory"),
                        ("Bộ nhớ trong", "128 GB / 256 GB / 512 GB"),
                        ("Camera", "Sau 12MP Wide quay 4K | Trước 12MP Ultra Wide cạnh ngang Center Stage"),
                        ("Dung lượng pin", "28.93 Wh (Lên đến 10 giờ sử dụng liên tục), sạc nhanh USB-C 20W"),
                        ("Hệ điều hành", "iPadOS 17 / iPadOS 18"),
                        ("Kết nối", "Wi-Fi 6E (tùy chọn 5G), Bluetooth 5.3, Cổng USB-C 3.1 Gen 2 (10Gbps)")
                    },
                    new[] {
                        "Hiệu năng vượt trội từ chip Apple M2 nhanh hơn gần 50% so với thế hệ trước, xử lý mượt đồ họa và AI.",
                        "Tương thích hoàn hảo với bút Apple Pencil Pro thế hệ mới và bàn phím Magic Keyboard.",
                        "Camera trước 12MP đặt trên cạnh ngang tích hợp tính năng Trung tâm màn hình (Center Stage) tự căn khung hình.",
                        "Kết nối Wi-Fi 6E tốc độ gấp đôi thế hệ trước cùng cổng USB-C truyền dữ liệu nhanh 10Gbps."
                    }
                ),
                [34] = (
                    new[] {
                        ("Màn hình", "11 inch Liquid Retina IPS, True Tone, dải màu P3, lớp phủ chống phản chiếu"),
                        ("Bộ vi xử lý", "Apple M2 (8 nhân CPU, 9 nhân GPU, 16 nhân Neural Engine)"),
                        ("RAM", "8 GB Unified Memory"),
                        ("Bộ nhớ trong", "128 GB / 256 GB / 512 GB"),
                        ("Camera", "Sau 12MP Wide quay 4K | Trước 12MP Ultra Wide cạnh ngang Center Stage"),
                        ("Dung lượng pin", "28.93 Wh (Lên đến 10 giờ sử dụng liên tục), sạc nhanh USB-C 20W"),
                        ("Hệ điều hành", "iPadOS 17 / iPadOS 18"),
                        ("Kết nối", "Wi-Fi 6E (tùy chọn 5G), Bluetooth 5.3, Cổng USB-C 3.1 Gen 2 (10Gbps)")
                    },
                    new[] {
                        "Hiệu năng vượt trội từ chip Apple M2 nhanh hơn gần 50% so với thế hệ trước, xử lý mượt đồ họa và AI.",
                        "Tương thích hoàn hảo với bút Apple Pencil Pro thế hệ mới và bàn phím Magic Keyboard.",
                        "Camera trước 12MP đặt trên cạnh ngang tích hợp tính năng Trung tâm màn hình (Center Stage) tự căn khung hình.",
                        "Kết nối Wi-Fi 6E tốc độ gấp đôi thế hệ trước cùng cổng USB-C truyền dữ liệu nhanh 10Gbps."
                    }
                ),
                [28] = (
                    new[] {
                        ("Màn hình", "12.4 inch LCD 3K (3048 x 2032) tỷ lệ 3:2, tần số quét 144Hz, độ sáng 900 nits"),
                        ("Bộ vi xử lý", "Qualcomm Snapdragon 8 Gen 2 (4nm)"),
                        ("RAM", "8 GB / 12 GB LPDDR5X"),
                        ("Bộ nhớ trong", "128 GB / 256 GB / 512 GB UFS 4.0"),
                        ("Camera", "Sau 50MP + Cảm biến độ sâu 2MP | Trước 32MP FocusFrame"),
                        ("Dung lượng pin", "10.000 mAh, công nghệ sạc thần tốc 120W HyperCharge"),
                        ("Hệ điều hành", "Xiaomi HyperOS (Android 14)"),
                        ("Kết nối", "Wi-Fi 7, Bluetooth 5.3, NFC, USB-C 3.2 Gen 1, Hệ thống 6 loa Dolby Atmos")
                    },
                    new[] {
                        "Màn hình 12.4 inch độ phân giải 3K sắc nét với tỷ lệ khung hình 3:2 tối ưu cho đọc tài liệu và làm việc.",
                        "Tần số quét AdaptiveSync 144Hz siêu mượt kết hợp 6 loa âm thanh vòm chuẩn Dolby Atmos.",
                        "Viên pin lớn 10.000 mAh đi kèm sạc siêu nhanh 120W nạp đầy 100% pin chỉ trong 35 phút.",
                        "Thiết kế kim loại nguyên khối sang trọng mỏng 6.26mm, kết nối liền mạch đa thiết bị trên HyperOS."
                    }
                ),

                // 6. PHỤ KIỆN (CategoryId = 6 - Chỉ lưu thông số phù hợp với phụ kiện) [11] = (
                    new[] {
                        ("Loại phụ kiện", "Cáp sạc và truyền dữ liệu bọc dù cao cấp"),
                        ("Công suất / Dung lượng", "Sạc nhanh PD 3.1 tối đa 140W (28V - 5A), tốc độ truyền dữ liệu 480Mbps"),
                        ("Chuẩn kết nối", "USB Type-C sang USB Type-C (tích hợp chip E-Marker thông minh)"),
                        ("Thiết bị tương thích", "MacBook Pro/Air, iPad Pro/Air, iPhone 15/16 Series, Samsung Galaxy, Laptop USB-C"),
                        ("Chất liệu / Kích thước", "Vỏ bện sợi Nylon kép chịu lực uốn gập 35.000 lần, chiều dài 1m / 2m")
                    },
                    new[] {
                        "Hỗ trợ chuẩn sạc nhanh Power Delivery 3.1 công suất lên đến 140W, sạc nhanh cho cả MacBook Pro 16 inch.",
                        "Tích hợp chip E-Marker tự động điều chỉnh dòng điện an toàn, chống quá nhiệt và bảo vệ pin thiết bị.",
                        "Lớp vỏ bện sợi Nylon siêu bền vượt qua thử nghiệm 35.000 lần uốn gập và chịu lực kéo 80kg."
                    }
                ),
                [29] = (
                    new[] {
                        ("Loại phụ kiện", "Đế sạc không dây từ tính MagSafe chuẩn Qi2"),
                        ("Công suất / Dung lượng", "Sạc nhanh không dây tối đa 15W"),
                        ("Chuẩn kết nối", "Đầu vào USB Type-C (khuyến nghị củ sạc PD từ 20W trở lên)"),
                        ("Thiết bị tương thích", "iPhone 12 / 13 / 14 / 15 / 16 Series, AirPods Pro / AirPods có hộp sạc MagSafe"),
                        ("Chất liệu / Kích thước", "Khung nhôm nguyên khối tản nhiệt, dây cáp bọc dù liền dài 1.5m")
                    },
                    new[] {
                        "Chứng nhận chuẩn sạc không dây Qi2 mới nhất cung cấp công suất 15W ổn định tương đương MagSafe chính hãng.",
                        "Vòng nam châm từ tính lực hút mạnh giúp tự động căn chỉnh đúng tâm sạc ngay khi đặt máy lên.",
                        "Công nghệ kiểm soát nhiệt độ ActiveShield 2.0 đo nhiệt độ 3 triệu lần mỗi ngày bảo vệ tuổi thọ pin."
                    }
                ),
                [30] = (
                    new[] {
                        ("Loại phụ kiện", "Pin sạc dự phòng công suất cao tích hợp màn hình thông minh"),
                        ("Công suất / Dung lượng", "24.000 mAh (86.4Wh) | Công suất sạc 2 chiều tối đa 140W PD 3.1"),
                        ("Chuẩn kết nối", "2 cổng USB-C (In/Out 140W) + 1 cổng USB-A (Out 18W)"),
                        ("Thiết bị tương thích", "MacBook, Laptop Windows USB-C, iPad, iPhone, Samsung Galaxy, Nintendo Switch"),
                        ("Chất liệu / Kích thước", "Vỏ chống cháy PC+ABS phủ nhám kim loại, đạt chuẩn mang lên máy bay (<100Wh)")
                    },
                    new[] {
                        "Dung lượng lớn 24.000 mAh cùng công suất 140W sạc đầy 50% MacBook Pro 16 inch chỉ trong 28 phút.",
                        "Trang bị màn hình kỹ thuật số thông minh hiển thị chính xác phần trăm pin, công suất W và thời gian sạc còn lại.",
                        "Sạc đồng thời 3 thiết bị cùng lúc qua 2 cổng USB-C và 1 cổng USB-A, đạt chuẩn an toàn hàng không quốc tế."
                    }
                ),
                [31] = (
                    new[] {
                        ("Loại phụ kiện", "Ốp lưng bảo vệ tích hợp vòng từ tính MagSafe"),
                        ("Chuẩn kết nối", "Vòng nam châm MagSafe 38 viên lực hút N52"),
                        ("Thiết bị tương thích", "Thiết kế chuẩn xác dành riêng cho iPhone 16 Pro Max (6.9 inch)"),
                        ("Chất liệu / Kích thước", "Silicone lỏng cao cấp / Polycarbonate chống ố vàng, lót sợi Microfiber mềm bên trong")
                    },
                    new[] {
                        "Ôm khít hoàn hảo thân máy iPhone 16 Pro Max với nút bấm kim loại nhạy bén và khe cắt chuẩn xác cho nút Camera Control.",
                        "Viền gờ nhô cao 1.5mm xung quanh cụm camera và 1.2mm quanh màn hình giúp chống trầy xước khi đặt trên mặt bàn.",
                        "Tích hợp vòng từ tính MagSafe siêu mạnh tương thích mọi loại sạc không dây, ví da và giá đỡ từ tính."
                    }
                ),
                [32] = (
                    new[] {
                        ("Loại phụ kiện", "Kính cường lực bảo vệ màn hình (Hộp 2 miếng kèm khung tự dán)"),
                        ("Thiết bị tương thích", "iPhone 16 Series"),
                        ("Chất liệu / Kích thước", "Kính aluminosilicate cao cấp độ cứng 9H, mỏng 0.33mm, vát cạnh 2.5D")
                    },
                    new[] {
                        "Độ cứng chuẩn 9H chịu lực va đập gấp 5 lần kính thường, bảo vệ màn hình tối đa khi rơi rớt.",
                        "Lớp phủ Nano Oleophobic mạ điện hạn chế tối đa bám vân tay, mồ hôi và giữ cảm ứng mượt như màn hình gốc.",
                        "Đi kèm khung căn chỉnh tự động thông minh giúp tự dán tại nhà chuẩn xác 100% không bọt khí."
                    }
                )
            };

            var allProducts = db.Products
                .Include(p => p.Specs)
                .Include(p => p.Highlights)
                .ToList();

            foreach (var p in allProducts)
            {
                if (!catalogData.TryGetValue(p.Id, out var data)) continue;

                // Cập nhật lại bộ thông số kỹ thuật chuẩn theo đúng danh mục của sản phẩm
                if (p.Specs.Any()) db.ProductSpecs.RemoveRange(p.Specs);
                for (int i = 0; i < data.specs.Length; i++)
                {
                    db.ProductSpecs.Add(new ProductSpec
                    {
                        ProductId = p.Id,
                        SpecKey = data.specs[i].k,
                        SpecValue = data.specs[i].v,
                        SortOrder = i
                    });
                }

                // Cập nhật danh sách Đặc điểm nổi bật riêng cho sản phẩm
                if (p.Highlights.Any()) db.ProductHighlights.RemoveRange(p.Highlights);
                for (int i = 0; i < data.highlights.Length; i++)
                {
                    db.ProductHighlights.Add(new ProductHighlight
                    {
                        ProductId = p.Id,
                        Content = data.highlights[i],
                        SortOrder = i
                    });
                }
            }

            db.SaveChanges();
        }
    }
}
