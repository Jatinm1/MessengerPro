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

    /// <summary>
    /// Initializes a new instance of the AuthController with the required authentication service.
    /// </summary>
    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>
    /// Gets the current user's ID from the JWT token claims.
    /// Returns either 'NameIdentifier' or 'sub' claim value as a Guid.
    /// </summary>
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!);

    /// <summary>
    /// Registers a new user with the provided credentials.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = await _authService.RegisterAsync(
            request.UserName,
            request.DisplayName,
            request.Password);

        return Ok(user);
    }

    /// <summary>
    /// Authenticates an existing user and returns login response with tokens.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request.UserName, request.Password);
        return Ok(response);
    }

    /// <summary>
    /// Logs out the currently authenticated user by invalidating their refresh token.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(CurrentUserId);
        return Ok(new { message = "Logged out successfully" });
    }
}