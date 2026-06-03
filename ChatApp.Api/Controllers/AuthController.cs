ung ChatApp.Api.Helpers;
using ChatApp.Application.DTOs.Auth;
using ChatApp.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!);

    private Guid CurrentFamilyId => Guid.Parse(
        User.FindFirstValue("familyId")!);

    private string? ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? ClientUserAgent =>
        Request.Headers.UserAgent.ToString();

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = await _authService.RegisterAsync(
            request.UserName,
            request.DisplayName,
            request.Password,
            request.emailId);

        return Ok(new
        {
            user.UserId,
            user.UserName,
            user.DisplayName,
            user.Email
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(
            request.UserName,
            request.Password,
            request.DeviceName,
            ClientIp,
            ClientUserAgent);

        CookieHelper.SetAuthCookies(
            Response,
            Request.IsHttps,
            result.AccessToken,
            result.RefreshToken,
            result.DeviceId);

        return Ok(new
        {
            result.User,
            result.DeviceId,
            result.ExpiresIn
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refresh_token"];
        var deviceIdStr = Request.Cookies["device_id"];

        if (string.IsNullOrWhiteSpace(refreshToken) ||
            !Guid.TryParse(deviceIdStr, out var deviceId))
        {
            return Unauthorized(new
            {
                message = "No refresh token present."
            });
        }

        var result = await _authService.RefreshAsync(
            refreshToken,
            deviceId,
            ClientIp,
            ClientUserAgent);

        CookieHelper.SetAuthCookies(
            Response,
            Request.IsHttps,
            result.AccessToken,
            result.RefreshToken,
            result.DeviceId);

        return Ok(new
        {
            result.User,
            result.DeviceId,
            result.ExpiresIn
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(
            CurrentUserId,
            CurrentFamilyId);

        CookieHelper.ClearAuthCookies(Response);

        return Ok(new
        {
            message = "Logged out successfully."
        });
    }

    [HttpPost("logout/all")]
    [Authorize]
    public async Task<IActionResult> GlobalLogout()
    {
        await _authService.GlobalLogoutAsync(CurrentUserId);

        CookieHelper.ClearAuthCookies(Response);

        return Ok(new
        {
            message = "Logged out from all devices."
        });
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _authService.GetSessionsAsync(
            CurrentUserId,
            CurrentFamilyId);

        return Ok(sessions);
    }

    [HttpDelete("sessions/{familyId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid familyId)
    {
        await _authService.RevokeSessionAsync(
            CurrentUserId,
            familyId);

        return Ok(new
        {
            message = "Session revoked."
        });
    }
}