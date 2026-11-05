using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IAuthService
    {
        Task<bool> RegisterAsync(UserRegisterDto dto);
        Task<(string? token, UserProfileDto? profile)> LoginAsync(UserLoginDto dto);
        Task<UserProfileDto?> GetProfileAsync(int userId);
        Task<(bool success, string message, UserProfileDto? profile)> UpdateProfileAsync(int userId, UpdateProfileDto dto);
        Task<(bool success, string message)> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    }
}
