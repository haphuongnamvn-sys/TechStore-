using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("image")]
        [Authorize(Roles = "admin,warehouse")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file hình ảnh hợp lệ" });

            // Giới hạn 10MB
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Dung lượng ảnh tối đa là 10MB" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { success = false, message = "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp, .gif" });

            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "products");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/products/{fileName}";
            return Ok(new { success = true, url, fileName });
        }

        [HttpPost("review-media")]
        [Authorize]
        public async Task<IActionResult> UploadReviewMedia([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file hình ảnh hoặc video" });

            if (files.Count > 6)
                return BadRequest(new { success = false, message = "Chỉ cho phép tải lên tối đa 5 ảnh và 1 video một lúc" });

            var allowedImg = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var allowedVideo = new[] { ".mp4", ".webm", ".mov", ".m4v" };

            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "reviews");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uploadedList = new List<object>();
            int imageCount = 0;
            int videoCount = 0;

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                bool isImg = allowedImg.Contains(ext);
                bool isVid = allowedVideo.Contains(ext);

                if (!isImg && !isVid)
                {
                    return BadRequest(new { success = false, message = $"Định dạng '{ext}' không được hỗ trợ. Chỉ chấp nhận ảnh (jpg, png, webp) hoặc video (mp4, webm, mov)." });
                }

                if (isImg)
                {
                    imageCount++;
                    if (imageCount > 5)
                        return BadRequest(new { success = false, message = "Chỉ được tải lên tối đa 5 hình ảnh" });
                    if (file.Length > 10 * 1024 * 1024)
                        return BadRequest(new { success = false, message = $"Ảnh '{file.FileName}' vượt quá dung lượng 10MB" });
                }
                else if (isVid)
                {
                    videoCount++;
                    if (videoCount > 1)
                        return BadRequest(new { success = false, message = "Chỉ được tải lên tối đa 1 video" });
                    if (file.Length > 60 * 1024 * 1024)
                        return BadRequest(new { success = false, message = $"Video '{file.FileName}' vượt quá dung lượng 60MB" });
                }

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var url = $"/uploads/reviews/{fileName}";
                uploadedList.Add(new
                {
                    url,
                    fileName,
                    type = isImg ? "image" : "video"
                });
            }

            return Ok(new { success = true, files = uploadedList });
        }
    }
}
