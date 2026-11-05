using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _context;

        public CartService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CartItemResponseDto>> GetCartAsync(int userId)
        {
            var items = await _context.Carts
                .Include(c => c.Product)
                    .ThenInclude(p => p!.Images)
                .Include(c => c.Product)
                    .ThenInclude(p => p!.Variants)
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.AddedAt)
                .ToListAsync();

            return items.Select(MapToDto).ToList();
        }

        public async Task<CartItemResponseDto?> AddToCartAsync(int userId, AddToCartDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null) return null;

            // Sản phẩm có biến thể: bắt buộc chọn đúng phiên bản + màu và kiểm tra tồn kho theo biến thể
            var variant = FindVariant(product, dto.Variant, dto.Color);
            if (product.Variants.Any() && variant == null) return null;
            var available = variant?.StockQuantity ?? product.StockQuantity;
            if (available < dto.Quantity) return null;

            // If already in cart with same Variant and Color, increase quantity
            var existing = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId 
                    && c.ProductId == dto.ProductId 
                    && c.Variant == dto.Variant 
                    && c.Color == dto.Color);

            if (existing != null)
            {
                existing.Quantity = Math.Min(existing.Quantity + dto.Quantity, available);
                await _context.SaveChangesAsync();
                existing.Product = product;
                return MapToDto(existing);
            }

            var cartItem = new Cart
            {
                UserId    = userId,
                ProductId = dto.ProductId,
                Quantity  = dto.Quantity,
                Variant   = dto.Variant,
                Color     = dto.Color,
                AddedAt   = DateTime.UtcNow
            };

            _context.Carts.Add(cartItem);
            await _context.SaveChangesAsync();
            cartItem.Product = product;
            return MapToDto(cartItem);
        }

        public async Task<CartItemResponseDto?> UpdateCartItemAsync(int userId, int cartItemId, UpdateCartDto dto)
        {
            var cartItem = await _context.Carts
                .Include(c => c.Product)
                    .ThenInclude(p => p!.Images)
                .Include(c => c.Product)
                    .ThenInclude(p => p!.Variants)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem == null) return null;

            var cartVariant = FindVariant(cartItem.Product!, cartItem.Variant, cartItem.Color);
            cartItem.Quantity = cartVariant != null && dto.Quantity > cartVariant.StockQuantity && cartVariant.StockQuantity > 0
                ? cartVariant.StockQuantity
                : dto.Quantity;
            await _context.SaveChangesAsync();
            return MapToDto(cartItem);
        }

        public async Task<bool> RemoveCartItemAsync(int userId, int cartItemId)
        {
            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem == null) return false;

            _context.Carts.Remove(cartItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ClearCartAsync(int userId)
        {
            var items = await _context.Carts.Where(c => c.UserId == userId).ToListAsync();
            _context.Carts.RemoveRange(items);
            await _context.SaveChangesAsync();
        }

        private static ProductVariant? FindVariant(Product product, string? variantName, string? colorName)
            => product.Variants.FirstOrDefault(v => v.VariantName == variantName && v.ColorName == colorName);

        private static CartItemResponseDto MapToDto(Cart c)
        {
            var variant = FindVariant(c.Product!, c.Variant, c.Color);
            var price     = variant != null ? (variant.OriginalPrice ?? variant.Price) : c.Product!.Price;
            var salePrice = variant != null ? variant.Price : c.Product!.SalePrice;
            var effectivePrice = salePrice > 0 ? salePrice : price;
            return new CartItemResponseDto
            {
                CartItemId   = c.Id,
                ProductId    = c.ProductId,
                ProductName  = c.Product!.Name,
                ProductImage = variant?.ImageUrl
                               ?? c.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? c.Product.Images.FirstOrDefault()?.ImageUrl ?? "",
                Price        = price,
                SalePrice    = salePrice,
                Quantity     = c.Quantity,
                Subtotal     = effectivePrice * c.Quantity,
                Variant      = c.Variant,
                Color        = c.Color
            };
        }
    }
}

