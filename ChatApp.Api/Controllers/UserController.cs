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

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                             User.FindFirstValue("sub")!);

    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var profile = await _userService.GetUserProfileAsync(CurrentUserId);

        if (profile == null)
            return NotFound(new { error = "Profile not found" });

        return Ok(profile);
    }

    [HttpGet("profile/{userId}")]
    public async Task<IActionResult> GetUserProfile(Guid userId)
    {
        var profile = await _userService.GetUserProfileByIdAsync(userId, CurrentUserId);

        if (profile == null)
            return NotFound(new { error = "User not found" });

        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        await _userService.UpdateUserProfileAsync(
            CurrentUserId,
            request.DisplayName,
            request.ProfilePhotoUrl,
            request.Bio
        );

        return Ok(new { message = "Profile updated successfully" });
    }
}