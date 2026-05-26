using ChatApp.Application.DTOs.Auth;
using ChatApp.Application.DTOs.User;
using ChatApp.Application.Interfaces.IRepositories;
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
    private readonly IUserRepository _userRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IEmailService _emailService;

    /// <summary>
    /// Initializes a new instance of the UserController with user profile services.
    /// </summary>
    public UserController(IUserService userService, ICloudinaryService cloudinaryService, IEmailService emailService, IUserRepository userRepository)
    {
        _userService = userService;
        _cloudinaryService = cloudinaryService;
        _emailService = emailService;
        _userRepository = userRepository;
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

    // UserController.cs — add these endpoints

    /// <summary>
    /// Saves an encrypted backup of the user's private key (protected by a PIN).
    /// The server never sees the actual private key — only the encrypted blob.
    /// </summary>
    [HttpPost("key-backup")]
    public async Task<IActionResult> SaveKeyBackup([FromBody] SaveKeyBackupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EncryptedKeyBackup) ||
            string.IsNullOrWhiteSpace(request.Salt))
            return BadRequest(new { error = "EncryptedKeyBackup and Salt are required" });

        await _userService.SaveKeyBackupAsync(CurrentUserId, request.EncryptedKeyBackup, request.Salt);
        return Ok(new { message = "Key backup saved" });
    }

    /// <summary>
    /// Returns the encrypted key backup blob so a new device can attempt to import it.
    /// </summary>
    [HttpGet("key-backup")]
    public async Task<IActionResult> GetKeyBackup()
    {
        var backup = await _userService.GetKeyBackupAsync(CurrentUserId);
        if (backup == null)
            return NotFound(new { error = "No key backup found" });

        return Ok(backup);
    }

    // UserController.cs — add two endpoints

    /// <summary>
    /// Sends a 6-digit PIN to the user's registered email for device switch verification.
    /// </summary>
    [HttpPost("device-switch/request-pin")]
    public async Task<IActionResult> RequestDeviceSwitchPin()
    {
        var user = await _userRepository.GetByIdAsync(CurrentUserId);
        if (user == null) return NotFound();

        // Generate 6-digit PIN
        var pin = new Random().Next(100000, 999999).ToString();
        var expires = DateTime.UtcNow.AddMinutes(10);

        // Store hashed PIN and expiry
        var hashedPin = BCrypt.Net.BCrypt.HashPassword(pin);
        await _userService.SaveDeviceSwitchPinAsync(CurrentUserId, hashedPin, expires);

        // Send email
        await _emailService.SendDeviceSwitchPinAsync(user.Email!, user.DisplayName!, pin);

        return Ok(new { message = "PIN sent to your registered email" });
    }

    /// <summary>
    /// Verifies the PIN. On success, returns a one-time token the client
    /// uses to confirm the device switch.
    /// </summary>
    [HttpPost("device-switch/verify-pin")]
    public async Task<IActionResult> VerifyDeviceSwitchPin([FromBody] VerifyPinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { error = "PIN is required" });

        var result = await _userService.VerifyDeviceSwitchPinAsync(CurrentUserId, request.Pin);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { verified = true });
    }
}