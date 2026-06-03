// ============================================================
// ChatApp.Api/Controllers/SignalRTokenController.cs
// NEW FILE — Issues a short-lived token for SignalR negotiation
//            so the permanent access_token is never in a URL
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class SignalRTokenController : ControllerBase
{
    private readonly IConfiguration _cfg;

    public SignalRTokenController(IConfiguration cfg) => _cfg = cfg;

    /// <summary>
    /// Issues a short-lived (30-second) token for SignalR negotiation.
    /// The client uses this token ONLY during the initial /negotiate HTTP call.
    /// After negotiation, SignalR uses the WebSocket connection — no further tokens needed.
    /// </summary>
    [HttpPost("signalr-token")]
    public IActionResult GetSignalRToken()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")!;
        var userName = User.FindFirstValue("uname")!;
        var familyId = User.FindFirstValue("familyId")!;

        var jwtKey = _cfg["Jwt:Key"]!;
        var issuer = _cfg["Jwt:Issuer"]!;
        var audience = _cfg["Jwt:Audience"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("uname",    userName),
            new Claim("familyId", familyId),
            new Claim("purpose", "signalr-negotiate")
        };

        // Very short-lived — only needed for the negotiate handshake
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(30),
            signingCredentials: creds);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}
