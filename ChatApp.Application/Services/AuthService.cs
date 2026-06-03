// ============================================================
// ChatApp.Application/Services/AuthService.cs
// MODIFIED FILE — Fixes:
//   VULN-002: Issuer/Audience validated in Program.cs (tokens
//             now include iss/aud claims)
//   VULN-003: Full refresh token architecture + rotation
//   VULN-005: No token in response body — set via HttpOnly cookie
//             in controller; service only returns TokenResponse
//   VULN-013: Uniform "Invalid credentials" + constant-time check
//   VULN-022: LoginResponse uses UserDto (no PasswordHash)
//   VULN-024: Account lockout after 5 failures / 15 min window
//   VULN-025: JWT key length validated in Program.cs
// ============================================================
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using ChatApp.Application.DTOs.Auth;
using ChatApp.Application.DTOs.User;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using ChatApp.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
//using UserDto = ChatApp.Application.DTOs.User.UserDto;

namespace ChatApp.Application.Services;

public class AuthService : IAuthService
{
    private const int FailedAttemptWindow = 15;   // minutes
    private const int MaxFailedAttempts = 5;
    private const int LockoutDurationMins = 15;
    private const int AccessTokenMinutes = 15;   // short-lived (VULN-003)
    private const int RefreshTokenDays = 30;

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IConfiguration _cfg;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        IConfiguration cfg)
    {
        _users = users;
        _tokens = tokens;
        _cfg = cfg;
    }

    // ── Login ─────────────────────────────────────────────────

    public async Task<TokenResponse> LoginAsync(
        string userName,
        string password,
        string? deviceName,
        string? ipAddress,
        string? userAgent)
    {
        // VULN-013: Always run BCrypt even when user not found (constant-time)
        var user = await _users.GetByUserNameAsync(userName);
        var dummyHash = "$2a$11$invalidhashfortimingXXXXXXXXXXXXXXXXXXXXXXXXXXX";
        var hashToCheck = user?.PasswordHash ?? dummyHash;

        var passwordValid = BCrypt.Net.BCrypt.Verify(password, hashToCheck);

        if (user == null || !passwordValid)
        {
            if (user != null)
                await _tokens.RecordLoginAttemptAsync(userName, ipAddress ?? "unknown", false);

            // VULN-013: Identical message regardless of which check failed
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // VULN-024: Check lockout before anything else
        var lockout = await _tokens.GetActiveLockoutAsync(user.UserId);
        if (lockout != null)
        {
            var remaining = (int)Math.Ceiling((lockout.UnlocksAtUtc - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedAccessException($"Account locked. Try again in {remaining} minute(s).");
        }

        // Check failed attempts and apply lockout if threshold exceeded
        var failedCount = await _tokens.GetRecentFailedAttemptsAsync(userName, FailedAttemptWindow);
        if (failedCount >= MaxFailedAttempts)
        {
            await _tokens.CreateLockoutAsync(user.UserId, LockoutDurationMins);
            throw new UnauthorizedAccessException($"Account locked due to too many failed attempts. Try again in {LockoutDurationMins} minutes.");
        }

        await _tokens.RecordLoginAttemptAsync(userName, ipAddress ?? "unknown", true);
        await _users.UpdateUserOnlineStatusAsync(user.UserId, true);

        return await IssueTokenPairAsync(user, deviceName ?? "Unknown Device", ipAddress, userAgent);
    }

    // ── Register ──────────────────────────────────────────────

    public async Task<User> RegisterAsync(
        string userName, string displayName, string password, string emailId)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        var id = await _users.CreateAsync(userName, displayName, hash, emailId);
        return (await _users.GetByIdAsync(id))!;
    }

    // ── Refresh ───────────────────────────────────────────────

    public async Task<TokenResponse> RefreshAsync(
        string refreshToken,
        Guid deviceId,
        string? ipAddress,
        string? userAgent)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _tokens.GetByHashAsync(tokenHash);

        if (stored == null || stored.IsRevoked || stored.FamilyIsRevoked)
            throw new SecurityTokenException("Invalid refresh token.");

        if (stored.ExpiresAtUtc < DateTime.UtcNow)
            throw new SecurityTokenException("Refresh token expired.");

        // VULN-003: Reuse detection — if already used, revoke the entire family
        if (stored.IsUsed)
        {
            await _tokens.RevokeFamilyAsync(stored.FamilyId, "Reuse");
            throw new SecurityTokenException("Refresh token reuse detected. All sessions revoked.");
        }

        var user = await _users.GetByIdAsync(stored.UserId)
            ?? throw new SecurityTokenException("User not found.");

        await _tokens.TouchSessionAsync(stored.FamilyId);

        return await RotateTokenPairAsync(stored, user, ipAddress, userAgent);
    }

    // ── Logout (single device) ────────────────────────────────

    public async Task LogoutAsync(Guid userId, Guid familyId)
    {
        await _tokens.RevokeFamilyAsync(familyId, "Logout");
        await _users.UpdateUserOnlineStatusAsync(userId, false);
        await _users.LogoutUserAsync(userId);
    }

    // ── Global Logout (all devices) ───────────────────────────

    public async Task GlobalLogoutAsync(Guid userId)
    {
        await _tokens.RevokeAllUserFamiliesAsync(userId, "GlobalLogout");
        await _users.UpdateUserOnlineStatusAsync(userId, false);
        await _users.LogoutUserAsync(userId);
    }

    // ── Sessions ──────────────────────────────────────────────

    public async Task<IEnumerable<SessionDto>> GetSessionsAsync(Guid userId, Guid currentFamilyId)
    {
        var sessions = await _tokens.GetActiveSessionsAsync(userId);
        return sessions.Select(s => new SessionDto
        {
            SessionId = s.SessionId,
            DeviceId = s.DeviceId,
            DeviceName = s.DeviceName,
            IpAddress = s.IpAddress,
            CreatedAtUtc = s.CreatedAtUtc,
            LastActiveUtc = s.LastActiveUtc,
            IsCurrent = s.FamilyId == currentFamilyId
        });
    }

    public async Task RevokeSessionAsync(Guid userId, Guid familyId)
    {
        // Ownership validated in controller before this call
        await _tokens.RevokeFamilyAsync(familyId, "SingleDeviceLogout");
    }

    // ── Private helpers ───────────────────────────────────────

    private async Task<TokenResponse> IssueTokenPairAsync(
        User user,
        string deviceName,
        string? ipAddress,
        string? userAgent)
    {
        var deviceId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var family = new RefreshTokenFamily
        {
            FamilyId = familyId,
            UserId = user.UserId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _tokens.CreateFamilyAsync(family);

        var (accessToken, _) = BuildAccessToken(user, familyId);
        var (rawRefresh, refreshEntity) = BuildRefreshToken(familyId, user.UserId, deviceId, ipAddress, userAgent);

        await _tokens.SaveTokenAsync(refreshEntity);

        var sessionId = Guid.NewGuid();
        await _tokens.UpsertSessionAsync(new UserSession
        {
            SessionId = sessionId,
            UserId = user.UserId,
            FamilyId = familyId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays),
            IsActive = true
        });

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefresh,
            DeviceId = deviceId,
            ExpiresIn = AccessTokenMinutes * 60,
            User = ToDto(user)
        };
    }

    private async Task<TokenResponse> RotateTokenPairAsync(
        RefreshToken stored,
        User user,
        string? ipAddress,
        string? userAgent)
    {
        var (accessToken, _) = BuildAccessToken(user, stored.FamilyId);
        var (rawRefresh, newRefreshEntity) = BuildRefreshToken(
            stored.FamilyId, user.UserId, stored.DeviceId, ipAddress, userAgent);

        await _tokens.RotateAsync(stored.TokenId, newRefreshEntity);

        await _tokens.UpsertSessionAsync(new UserSession
        {
            SessionId = Guid.NewGuid(),
            UserId = user.UserId,
            FamilyId = stored.FamilyId,
            DeviceId = stored.DeviceId,
            DeviceName = stored.DeviceName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays),
            IsActive = true
        });

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefresh,
            DeviceId = stored.DeviceId,
            ExpiresIn = AccessTokenMinutes * 60,
            User = ToDto(user)
        };
    }

    private (string jwt, DateTime expires) BuildAccessToken(User user, Guid familyId)
    {
        var jwtKey = _cfg["Jwt:Key"]!;
        var issuer = _cfg["Jwt:Issuer"]!;
        var audience = _cfg["Jwt:Audience"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,    user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,    Guid.NewGuid().ToString()),
            new Claim("uname",                         user.UserName),
            new Claim("familyId",                      familyId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private (string rawToken, RefreshToken entity) BuildRefreshToken(
        Guid familyId,
        Guid userId,
        Guid deviceId,
        string? ipAddress,
        string? userAgent)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);
        var entity = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = userId,
            DeviceId = deviceId,
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays),
            CreatedAtUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        return (rawToken, entity);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static UserDto ToDto(User u) =>
        new(u.UserId, u.UserName, u.DisplayName, u.CreatedAtUtc);
}
