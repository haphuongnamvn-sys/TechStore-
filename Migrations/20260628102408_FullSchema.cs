using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Web.Migrations
{
    /// <inheritdoc />
    public partial class FullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    DiscountType = table.Column<string>(type: "TEXT", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxUsage = table.Column<int>(type: "INTEGER", nullable: false),
                    UsedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "customer"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    BrandId = table.Column<int>(type: "INTEGER", nullable: false),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingAvg = table.Column<double>(type: "REAL", nullable: false),
                    Badge = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrderCode = table.Column<string>(type: "TEXT", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ShippingAddress = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentStatus = table.Column<string>(type: "TEXT", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Carts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecKey = table.Column<string>(type: "TEXT", nullable: false),
                    SpecValue = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reviews_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "Id", "Country", "LogoUrl", "Name" },
                values: new object[,]
                {
                    { 1, "USA", "https://upload.wikimedia.org/wikipedia/commons/f/fa/Apple_logo_black.svg", "Apple" },
                    { 2, "South Korea", "https://upload.wikimedia.org/wikipedia/commons/2/24/Samsung_Logo.svg", "Samsung" },
                    { 3, "Japan", "https://upload.wikimedia.org/wikipedia/commons/c/ca/Sony_logo.svg", "Sony" },
                    { 4, "Taiwan", "https://upload.wikimedia.org/wikipedia/commons/2/2e/ASUS_Logo.svg", "ASUS" },
                    { 5, "China", "https://upload.wikimedia.org/wikipedia/commons/2/29/Xiaomi_logo.svg", "Xiaomi" },
                    { 6, "USA", "https://upload.wikimedia.org/wikipedia/commons/a/a0/Anker_logo.svg", "Anker" }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "ImageUrl", "Name", "ParentId", "Slug" },
                values: new object[,]
                {
                    { 1, "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=200", "Điện thoại", null, "dien-thoai" },
                    { 2, "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=200", "Laptop", null, "laptop" },
                    { 3, "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=200", "Âm thanh", null, "am-thanh" },
                    { 4, "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?w=200", "Đồng hồ thông minh", null, "dong-ho-thong-minh" },
                    { 5, "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=200", "Máy tính bảng", null, "may-tinh-bang" },
                    { 6, "https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=200", "Phụ kiện", null, "phu-kien" }
                });

            migrationBuilder.InsertData(
                table: "Promotions",
                columns: new[] { "Id", "Code", "DiscountType", "DiscountValue", "EndDate", "MaxUsage", "MinOrderValue", "StartDate", "UsedCount" },
                values: new object[,]
                {
                    { 1, "WELCOME10", "percent", 10m, new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 100, 500000m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 0 },
                    { 2, "SALE200K", "fixed", 200000m, new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 50, 2000000m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 0 }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Badge", "BrandId", "CategoryId", "CreatedAt", "Description", "Name", "Price", "RatingAvg", "SalePrice", "Slug", "StockQuantity" },
                values: new object[,]
                {
                    { 1, "sale", 1, 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Chip A17 Pro, màn hình Super Retina XDR 6.7\", titanium frame.", "iPhone 15 Pro Max", 34990000m, 4.9000000000000004, 29990000m, "iphone-15-pro-max", 50 },
                    { 2, "sale", 2, 1, new DateTime(2024, 1, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "Bút S-Pen tích hợp, camera 200MP, màn hình Dynamic AMOLED 6.8\".", "Samsung Galaxy S24 Ultra", 30990000m, 4.7999999999999998, 26490000m, "samsung-galaxy-s24-ultra", 40 },
                    { 3, "hot", 1, 2, new DateTime(2024, 1, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), "Chip M3 mạnh mẽ, pin 18h, màn hình Liquid Retina 13.6\".", "MacBook Air M3", 32000000m, 4.9000000000000004, 28990000m, "macbook-air-m3", 10 },
                    { 4, "sale", 3, 3, new DateTime(2024, 1, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "Chống ồn ANC tốt nhất thị trường, âm thanh Hi-Res, pin 30h.", "Sony WH-1000XM5", 9990000m, 4.7999999999999998, 7490000m, "sony-wh-1000xm5", 80 },
                    { 5, "sale", 1, 4, new DateTime(2024, 1, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Màn hình Always-On Retina, S9 chip, đo oxy máu, ECG.", "Apple Watch Series 9", 12990000m, 4.7000000000000002, 9990000m, "apple-watch-series-9", 30 },
                    { 6, "new", 1, 5, new DateTime(2024, 1, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), "Chip M4 cực mạnh, màn hình OLED Ultra Retina XDR 11\".", "iPad Pro M4", 28000000m, 4.9000000000000004, 24990000m, "ipad-pro-m4", 25 },
                    { 7, "hot", 4, 2, new DateTime(2024, 1, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), "Gaming laptop AMD Ryzen 9, RTX 4070, màn hình 2.5K 165Hz.", "ASUS ROG Zephyrus G14", 42000000m, 4.7000000000000002, 35990000m, "asus-rog-zephyrus-g14", 15 },
                    { 8, "sale", 1, 3, new DateTime(2024, 1, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), "ANC thế hệ mới, Adaptive Audio, Transparency mode nâng cao.", "AirPods Pro 2nd Gen", 7490000m, 4.7999999999999998, 5990000m, "airpods-pro-2nd-gen", 100 },
                    { 9, "sale", 2, 5, new DateTime(2024, 1, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), "Màn hình Dynamic AMOLED 12.4\" 120Hz, chip Snapdragon 8 Gen 2.", "Samsung Galaxy Tab S9+", 23990000m, 4.7000000000000002, 19990000m, "samsung-galaxy-tab-s9-plus", 20 },
                    { 10, "sale", 5, 4, new DateTime(2024, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Wear OS, AMOLED 1.43\", Snapdragon W5+, GPS, SpO2, NFC.", "Xiaomi Watch 2 Pro", 4290000m, 4.5, 3490000m, "xiaomi-watch-2-pro", 60 },
                    { 11, "sale", 6, 6, new DateTime(2024, 1, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sạc nhanh 140W, dài 1.8m, bọc dù cao cấp.", "Dây sạc USB-C 140W", 399000m, 4.5999999999999996, 290000m, "day-sac-usb-c-140w", 200 },
                    { 12, "new", 5, 1, new DateTime(2024, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "Camera Leica Professional, Snapdragon 8 Gen 3.", "Xiaomi 14 Ultra", 25990000m, 4.7000000000000002, 21990000m, "xiaomi-14-ultra", 35 }
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ImageUrl", "IsPrimary", "ProductId", "SortOrder" },
                values: new object[,]
                {
                    { 1, "https://images.unsplash.com/photo-1695048133142-1a20484d2569?w=600", true, 1, 0 },
                    { 2, "https://images.unsplash.com/photo-1632661674596-df8be070a5c5?w=600", false, 1, 1 },
                    { 3, "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?w=600", true, 2, 0 },
                    { 4, "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=600", true, 3, 0 },
                    { 5, "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=600", true, 4, 0 },
                    { 6, "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?w=600", true, 5, 0 },
                    { 7, "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=600", true, 6, 0 },
                    { 8, "https://images.unsplash.com/photo-1603302576837-37561b2e2302?w=600", true, 7, 0 },
                    { 9, "https://images.unsplash.com/photo-1588449668338-d15178222883?w=600", true, 8, 0 },
                    { 10, "https://images.unsplash.com/photo-1561154464-82e9adf32764?w=600", true, 9, 0 },
                    { 11, "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=600", true, 10, 0 },
                    { 12, "https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=600", true, 11, 0 },
                    { 13, "https://images.unsplash.com/photo-1598327105666-5b89351aff97?w=600", true, 12, 0 }
                });

            migrationBuilder.InsertData(
                table: "ProductSpecs",
                columns: new[] { "Id", "ProductId", "SpecKey", "SpecValue" },
                values: new object[,]
                {
                    { 1, 1, "Chip", "Apple A17 Pro" },
                    { 2, 1, "Màn hình", "6.7\" Super Retina XDR OLED" },
                    { 3, 1, "Camera sau", "48MP + 12MP + 12MP" },
                    { 4, 1, "RAM", "8GB" },
                    { 5, 1, "Bộ nhớ", "256GB" },
                    { 6, 3, "Chip", "Apple M3 8-core CPU" },
                    { 7, 3, "RAM", "16GB Unified Memory" },
                    { 8, 3, "Ổ cứng", "512GB SSD" },
                    { 9, 3, "Pin", "18 giờ" },
                    { 10, 4, "Driver", "30mm" },
                    { 11, 4, "Kết nối", "Bluetooth 5.2" },
                    { 12, 4, "Pin", "30 giờ" },
                    { 13, 4, "Chống ồn", "ANC thế hệ mới" },
                    { 14, 7, "CPU", "AMD Ryzen 9 8945HS" },
                    { 15, 7, "GPU", "NVIDIA RTX 4070" },
                    { 16, 7, "RAM", "32GB DDR5" },
                    { 17, 7, "Màn hình", "14\" 2.5K 165Hz" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carts_ProductId",
                table: "Carts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderCode",
                table: "Orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecs_ProductId",
                table: "ProductSpecs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Code",
                table: "Promotions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderId",
                table: "Reviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProductId",
                table: "Reviews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "ProductSpecs");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
