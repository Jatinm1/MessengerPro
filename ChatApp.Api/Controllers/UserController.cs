using ChatApp.Application.DTOs.User;
using ChatApp.Application.Interfaces.IServices;
using ChatApp.Application.Services;
using ChatApp.Domain.ValueObjects;
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

    /// <summary>
    /// Initializes a new instance of the UserController with user profile services.
    /// </summary>
    public UserController(IUserService userService, ICloudinaryService cloudinaryService)
    {
        _userService = userService;
        _cloudinaryService = cloudinaryService;
    }

    /// <summary>
    /// Gets the current user's ID from the JWT token claims.
    /// Returns either 'NameIdentifier' or 'sub' claim value as a Guid.
    /// </summary>
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!
    );

    /// <summary>
    /// Retrieves the current user's complete profile information.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var profile = await _userService.GetUserProfileAsync(CurrentUserId);
        if (profile == null)
            return NotFound(new { error = "Profile not found" });

        return Ok(profile);
    }

    /// <summary>
    /// Retrieves another user's public profile information.
    /// </summary>
    [HttpGet("profile/{userId}")]
    public async Task<IActionResult> GetUserProfile(Guid userId)
    {
        var profile = await _userService.GetUserProfileByIdAsync(userId, CurrentUserId);
        if (profile == null)
            return NotFound(new { error = "User not found" });

        return Ok(profile);
    }

    /// <summary>
    /// Updates the current user's profile information (display name and bio).
    /// Does not handle profile photo updates.
    /// </summary>
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

    /// <summary>
    /// Uploads and sets a new profile photo for the current user.
    /// Maximum file size: 10MB. Allowed types: JPEG, PNG, GIF, WebP.
    /// </summary>
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

            // Update user's profile photo in database
            await _userService.UpdateProfilePhotoAsync(CurrentUserId, url);

            return Ok(new { url, publicId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Upload failed: {ex.Message}" });
        }


    }
    // UserController.cs — add these two endpoints

    /// <summary>
    /// Registers or updates the current user's RSA public key.
    /// Called once per device on first login after key generation.
    /// </summary>
    [HttpPost("public-key")]
    public async Task<IActionResult> RegisterPublicKey([FromBody] RegisterPublicKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PublicKeyJwk))
            return BadRequest(new { error = "Public key is required" });

        await _userService.SavePublicKeyAsync(CurrentUserId, request.PublicKeyJwk);
        return Ok(new { message = "Public key registered successfully" });
    }

    /// <summary>
    /// Fetches RSA public keys for a list of user IDs.
    /// Called before encrypting a message so we can encrypt the AES key
    /// for each recipient (and the sender themselves).
    /// </summary>
    [HttpPost("public-keys")]
    public async Task<IActionResult> GetPublicKeys([FromBody] GetPublicKeysRequest request)
    {
        if (request.UserIds == null || request.UserIds.Count == 0)
            return BadRequest(new { error = "At least one userId is required" });

        var keys = await _userService.GetPublicKeysAsync(request.UserIds);
        return Ok(keys);
    }
}