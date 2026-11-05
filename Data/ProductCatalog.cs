namespace Web.Data
{
    public record ColorDef(string Name, string Hex, string Slug);
    public record VersionDef(string Name, decimal Delta);

    /// <summary>
    /// Danh mục phiên bản (bộ nhớ) và màu sắc thực tế của từng sản phẩm.
    /// Ảnh theo màu nằm ở wwwroot/images/products/p{ImageProductId}-{slug}.png
    /// </summary>
    public record CatalogEntry(VersionDef[] Versions, ColorDef[] Colors, int? ImageProductId = null);

    public static class ProductCatalog
    {
        private static VersionDef[] V(params (string, decimal)[] v) => v.Select(x => new VersionDef(x.Item1, x.Item2)).ToArray();
        private static ColorDef C(string name, string hex, string slug) => new(name, hex, slug);

        private static readonly ColorDef[] IPadAirColors =
        {
            C("Xám Không Gian", "#525458", "xam-khong-gian"), C("Bạc Ánh Kim", "#dcdde0", "bac"), C("Xanh Dương", "#7ea6d2", "xanh"), C("Tím", "#a892cb", "tim")
        };
        private static readonly ColorDef[] MbpColors =
        {
            C("Đen Không Gian", "#2d2d30", "den-khong-gian"), C("Bạc", "#e3e4e5", "bac")
        };

        public static readonly Dictionary<int, CatalogEntry> Entries = new()
        {
            // Điện thoại [1] = new(V(("256GB", 0), ("512GB", 5000000), ("1TB", 11000000)),
                new[] { C("Titan Tự Nhiên", "#b3aca4", "titan-tu-nhien"), C("Titan Đen", "#3b3c3e", "titan-den"), C("Titan Trắng", "#e3e4e6", "titan-trang"), C("Titan Xanh", "#384b63", "titan-xanh") }),
            [13] = new(V(("256GB", 0), ("512GB", 5000000), ("1TB", 11000000)),
                new[] { C("Titan Sa Mạc", "#c5a88c", "sa-mac"), C("Titan Tự Nhiên", "#b3aca4", "tu-nhien"), C("Titan Đen", "#242526", "den"), C("Titan Trắng", "#edebe6", "trang") }),
            [2] = new(V(("256GB", 0), ("512GB", 4000000), ("1TB", 9000000)),
                new[] { C("Xám Titan", "#7a7b80", "xam-titan"), C("Đen Titan", "#2c2d30", "den-titan"), C("Tím Titan", "#685888", "tim-titan") }),
            [14] = new(V(("256GB", 0), ("512GB", 4000000), ("1TB", 9000000)),
                new[] { C("Xám Titan", "#7a7b80", "xam-titan"), C("Đen Titan", "#2c2d30", "den-titan"), C("Xanh Bạc Titan", "#7a9ab8", "xanh-titan") }),
            [12] = new(V(("256GB", 0), ("512GB", 3000000)),
                new[] { C("Đen", "#1f1f1f", "den"), C("Trắng", "#f2f2f2", "trang") }),
            [15] = new(V(("256GB", 0), ("512GB", 3000000)),
                new[] { C("Đen", "#1f1f1f", "den"), C("Trắng", "#f2f2f2", "trang") }),
            [16] = new(V(("128GB", 0), ("256GB", 2000000)),
                new[] { C("Xanh Navy", "#2b3a5c", "xanh-navy"), C("Tím Lilac", "#c8b6e2", "tim") }),
            [17] = new(V(("128GB", 0), ("256GB", 3000000), ("512GB", 8000000)),
                new[] { C("Hồng", "#eebec6", "hong"), C("Xanh Dương", "#9ec4e2", "xanh"), C("Đen", "#2c2d30", "den") }),

            // Laptop [3] = new(V(("16GB / 256GB", 0), ("24GB / 512GB", 6000000)),
                new[] { C("Xám Không Gian", "#7d7e80", "xam-khong-gian"), C("Bạc", "#e3e4e5", "bac"), C("Nửa Đêm", "#2c3d55", "nua-dem") }),
            [18] = new(V(("24GB / 512GB", 0), ("48GB / 1TB", 15000000)), MbpColors),
            [33] = new(V(("24GB / 512GB", 0), ("48GB / 1TB", 15000000)), MbpColors, 18),
            [19] = new(V(("16GB / 512GB", 0), ("16GB / 1TB", 2000000)),
                new[] { C("Bạc", "#d9dadc", "bac"), C("Xanh Quiet Blue", "#52749c", "xanh") }),
            [20] = new(V(("16GB / 512GB", 0), ("32GB / 1TB", 7000000)),
                new[] { C("Đen Carbon", "#1e2022", "den"), C("Xám Storm Grey", "#686c73", "xam") }),
            [21] = new(V(("16GB / 512GB", 0), ("32GB / 1TB", 5000000)),
                new[] { C("Xám Mecha", "#6b6f73", "xam-mecha"), C("Đen Jaeger", "#232526", "den") }),
            [7] = new(V(("16GB / 1TB", 0), ("32GB / 1TB", 8000000)),
                new[] { C("Xám Eclipse", "#5e6064", "xam-eclipse"), C("Trắng Moonstone", "#eaedf2", "trang-moonstone") }),

            // Âm thanh [4] = new(V(("Chính Hãng", 0)), new[] { C("Đen", "#1a1a1a", "den"), C("Bạc", "#d8d8d8", "bac") }),
            [8] = new(V(("USB-C (2024)", 0)), new[] { C("Trắng", "#f5f5f5", "trang"), C("Đen Midnight", "#242629", "den") }),
            [22] = new(V(("Chính Hãng", 0)), new[] { C("Đen", "#1a1a1a", "den"), C("Bạc Bạch Kim", "#dcdad5", "bac") }),
            [23] = new(V(("Chính Hãng", 0)), new[] { C("Bạc", "#cfd0d2", "bac"), C("Trắng", "#f5f5f5", "trang") }),
            [24] = new(V(("Chính Hãng", 0)), new[] { C("Đen", "#1a1a1a", "den"), C("Xanh Navy", "#28426e", "xanh") }),

            // Đồng hồ thông minh [5] = new(V(("41mm GPS", 0), ("45mm GPS", 1000000), ("45mm GPS + LTE", 4000000)),
                new[] { C("Midnight", "#1f242b", "midnight"), C("Starlight", "#eae6df", "starlight"), C("Hồng", "#ebb8c0", "hong") }),
            [25] = new(V(("49mm LTE", 0)),
                new[] { C("Titan Tự Nhiên", "#b9b4aa", "tu-nhien"), C("Titan Đen", "#2a2b2d", "den") }),
            [26] = new(V(("40mm Bluetooth", 0), ("44mm Bluetooth", 1000000), ("44mm LTE", 2500000)),
                new[] { C("Xanh Lá", "#547558", "xanh-la"), C("Bạc", "#d6d7d9", "bac") }),
            [10] = new(V(("Tiêu Chuẩn", 0)),
                new[] { C("Bạc", "#d0d1d3", "bac"), C("Đen", "#1f1f1f", "den") }),

            // Máy tính bảng [6] = new(V(("256GB Wi-Fi", 0), ("256GB Wi-Fi + 5G", 4000000), ("512GB Wi-Fi + 5G", 9000000)),
                new[] { C("Xám Không Gian", "#3a3c40", "xam-khong-gian"), C("Bạc Ánh Kim", "#dfe2e6", "bac") }),
            [27] = new(V(("128GB Wi-Fi", 0), ("256GB Wi-Fi + 5G", 4000000)), IPadAirColors),
            [34] = new(V(("128GB Wi-Fi", 0), ("256GB Wi-Fi + 5G", 4000000)), IPadAirColors, 27),
            [9] = new(V(("256GB Wi-Fi", 0), ("512GB Wi-Fi + 5G", 4000000)),
                new[] { C("Xám Graphite", "#4b4d50", "graphite"), C("Bạc Beige", "#d8cdbd", "be") }),
            [28] = new(V(("128GB Wi-Fi", 0), ("256GB Wi-Fi + 5G", 4000000)),
                new[] { C("Xám Không Gian", "#525458", "xam-khong-gian"), C("Bạc Ánh Kim", "#dfe2e6", "bac"), C("Xanh Dương", "#6a90bc", "xanh") }),

            // Phụ kiện [11] = new(V(("1m", 0), ("2m", 60000)), new[] { C("Trắng", "#f5f5f5", "trang"), C("Đen", "#1a1a1a", "den") }),
            [29] = new(V(("Tiêu Chuẩn", 0)), new[] { C("Trắng", "#f5f5f5", "trang"), C("Đen", "#1a1a1a", "den") }),
            [30] = new(V(("Tiêu Chuẩn", 0)), new[] { C("Đen", "#1a1a1a", "den"), C("Trắng Bạc", "#e2e5e9", "trang") }),
            [31] = new(V(("Tiêu Chuẩn", 0)),
                new[] { C("Đen", "#1a1a1a", "den"), C("Trong Suốt", "#e8eef2", "trong-suot"), C("Xanh Dương", "#4a74b5", "xanh") }),
            [32] = new(V(("Hộp 2 Miếng", 0)), new[] { C("Trong Suốt HD", "#e8eef2", "trong-suot"), C("Viền Đen Chống Nhìn Trộm", "#222428", "vien-den") }),
        };
    }
}
