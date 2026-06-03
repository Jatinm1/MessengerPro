// ============================================================
// ChatApp.Application/Interfaces/IRepositories/IRefreshTokenRepository.cs
// NEW FILE — VULN-003: Refresh token persistence abstraction
// ============================================================
using ChatApp.Domain.Entities;

namespace ChatApp.Application.Interfaces.IRepositories;

public interface IRefreshTokenRepository
{
    // Family management
    Task CreateFamilyAsync(RefreshTokenFamily family);
    Task RevokeFamilyAsync(Guid familyId, string reason);
    Task RevokeAllUserFamiliesAsync(Guid userId, string reason);

    // Token lifecycle
    Task SaveTokenAsync(RefreshToken token);
    Task<RefreshToken?> GetByHashAsync(string tokenHash);
    Task RotateAsync(Guid oldTokenId, RefreshToken newToken);

    // Sessions
    Task UpsertSessionAsync(UserSession session);
    Task<IEnumerable<UserSession>> GetActiveSessionsAsync(Guid userId);
    Task TouchSessionAsync(Guid familyId);

    // Lockout (VULN-024)
    Task RecordLoginAttemptAsync(string userName, string ipAddress, bool succeeded);
    Task<int> GetRecentFailedAttemptsAsync(string userName, int windowMinutes);
    Task CreateLockoutAsync(Guid userId, int durationMinutes);
    Task<AccountLockout?> GetActiveLockoutAsync(Guid userId);
}
