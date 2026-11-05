using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
        {
            var success = await _authService.RegisterAsync(dto);
            if (!success)
                return BadRequest(new { success = false, message = "Tài khoản hoặc email đã tồn tại!" });

            return Ok(new { success = true, message = "Đăng ký thành công!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            try
            {
                var (token, profile) = await _authService.LoginAsync(dto);
                if (token == null)
                    return BadRequest(new { success = false, message = "Sai tài khoản hoặc mật khẩu!" });

                return Ok(new { success = true, token, user = profile });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");
            var profile = await _authService.GetProfileAsync(userId);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");
            var (success, message, profile) = await _authService.UpdateProfileAsync(userId, dto);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message, user = profile });
        }

        [HttpPut("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");
            var (success, message) = await _authService.ChangePasswordAsync(userId, dto);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }
    }
}
