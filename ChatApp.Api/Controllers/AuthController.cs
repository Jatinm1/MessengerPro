using ChatApp.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    public record LoginReq(string UserName, string Password);
    public record RegisterReq(string UserName, string DisplayName, string Password);

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                             User.FindFirstValue("sub")!);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterReq r)
        => Ok(await _auth.RegisterAsync(r.UserName, r.DisplayName, r.Password));

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginReq r)
    {
        var (token, user) = await _auth.LoginAsync(r.UserName, r.Password);
        return Ok(new { token, user });
    }
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {

        await _auth.LogoutAsync(CurrentUserId);
        return Ok(new { message = "Logged out successfully" });
    }

}
