using Web.Models;

namespace Web.Services.Interfaces
{
    public interface ICartService
    {
        Task<List<CartItemResponseDto>> GetCartAsync(int userId);
        Task<CartItemResponseDto?> AddToCartAsync(int userId, AddToCartDto dto);
        Task<CartItemResponseDto?> UpdateCartItemAsync(int userId, int cartItemId, UpdateCartDto dto);
        Task<bool> RemoveCartItemAsync(int userId, int cartItemId);
        Task ClearCartAsync(int userId);
    }
}
