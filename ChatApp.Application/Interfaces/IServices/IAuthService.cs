// ============================================================
// ChatApp.Application/Interfaces/IServices/IAuthService.cs
// MODIFIED FILE — adds refresh token, session, device methods
// ============================================================
using ChatApp.Application.DTOs.Auth;
using ChatApp.Domain.Entities;

namespace ChatApp.Application.Interfaces.IServices;

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(string userName, string password, string? deviceName, string? ipAddress, string? userAgent);
    Task<User> RegisterAsync(string userName, string displayName, string password, string emailId);
    Task<TokenResponse> RefreshAsync(string refreshToken, Guid deviceId, string? ipAddress, string? userAgent);
    Task LogoutAsync(Guid userId, Guid familyId);
    Task GlobalLogoutAsync(Guid userId);
    Task<IEnumerable<SessionDto>> GetSessionsAsync(Guid userId, Guid currentFamilyId);
    Task RevokeSessionAsync(Guid userId, Guid familyId);
}
