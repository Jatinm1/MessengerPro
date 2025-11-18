using ChatApp.Application.Services;
using ChatApp.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICloudinaryService _cloudinaryService;

    public UserController(IUserService userService, ICloudinaryService cloudinaryService)
    {
        _userService = userService;
        _cloudinaryService = cloudinaryService;
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!
    );

    // GET: api/user/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var profile = await _userService.GetUserProfileAsync(CurrentUserId);
        if (profile == null)
            return NotFound(new { error = "Profile not found" });

        return Ok(profile);
    }

    // GET: api/user/profile/{userId}
    [HttpGet("profile/{userId}")]
    public async Task<IActionResult> GetUserProfile(Guid userId)
    {
        var profile = await _userService.GetUserProfileByIdAsync(userId, CurrentUserId);
        if (profile == null)
            return NotFound(new { error = "User not found" });

        return Ok(profile);
    }

    // PUT: api/user/profile (NO PHOTO UPDATE)
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        await _userService.UpdateUserProfileAsync(
            CurrentUserId,
            request.DisplayName,
            request.Bio
        );

        return Ok(new { message = "Profile updated successfully" });
    }

    // POST: api/user/profile/photo
    [HttpPost("profile/photo")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };

        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { error = "Invalid file type. Only images are allowed." });

        try
        {
            using var stream = file.OpenReadStream();
            var (url, publicId, error) = await _cloudinaryService.UploadImageAsync(stream, file.FileName);

            if (error != null)
                return BadRequest(new { error = $"Upload failed: {error}" });

            // Only update profile photo
            await _userService.UpdateProfilePhotoAsync(CurrentUserId, url);

            return Ok(new { url, publicId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Upload failed: {ex.Message}" });
        }
    }
}
