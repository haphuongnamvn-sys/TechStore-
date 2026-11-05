using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<bool> RegisterAsync(UserRegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
                return false;

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
                return false;

            var newUser = new User
            {
                Username = dto.Username,
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "customer"
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(string? token, UserProfileDto? profile)> LoginAsync(UserLoginDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());

            if (user == null) return (null, null);

            if (user.IsLocked)
                throw new InvalidOperationException("Tài khoản của bạn đã bị tạm khóa bởi Quản trị viên. Vui lòng liên hệ hỗ trợ!");

            bool isPasswordCorrect;
            if (user.Password.StartsWith("$2"))
            {
                isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);
            }
            else
            {
                isPasswordCorrect = (user.Password == dto.Password);
                if (isPasswordCorrect)
                {
                    // Automatically upgrade plain-text password to secure BCrypt hash
                    user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                    await _context.SaveChangesAsync();
                }
            }

            if (!isPasswordCorrect) return (null, null);

            var token = GenerateJwtToken(user);
            var profile = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Role = user.Role,
                IsLocked = user.IsLocked,
                CreatedAt = user.CreatedAt
            };

            return (token, profile);
        }

        public async Task<UserProfileDto?> GetProfileAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Role = user.Role,
                IsLocked = user.IsLocked,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<(bool success, string message, UserProfileDto? profile)> UpdateProfileAsync(int userId, UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return (false, "Không tìm thấy người dùng", null);

            var emailExists = await _context.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == dto.Email.ToLower());
            if (emailExists) return (false, "Email này đã được sử dụng bởi tài khoản khác", null);

            user.FullName = dto.FullName.Trim();
            user.Email = dto.Email.Trim();
            user.Phone = dto.Phone?.Trim() ?? "";
            user.Address = dto.Address?.Trim() ?? "";

            await _context.SaveChangesAsync();

            var profile = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Role = user.Role,
                IsLocked = user.IsLocked,
                CreatedAt = user.CreatedAt
            };

            return (true, "Cập nhật thông tin thành công!", profile);
        }

        public async Task<(bool success, string message)> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return (false, "Không tìm thấy người dùng");

            bool isCurrentCorrect;
            if (user.Password.StartsWith("$2"))
                isCurrentCorrect = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.Password);
            else
                isCurrentCorrect = (user.Password == dto.CurrentPassword);

            if (!isCurrentCorrect)
                return (false, "Mật khẩu hiện tại không chính xác");

            if (dto.NewPassword.Length < 6)
                return (false, "Mật khẩu mới phải có ít nhất 6 ký tự");

            user.Password = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
            return (true, "Đổi mật khẩu thành công!");
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                    new Claim("username", user.Username),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim("FullName", user.FullName),
                    new Claim("name", user.FullName),
                    new Claim("email", user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
