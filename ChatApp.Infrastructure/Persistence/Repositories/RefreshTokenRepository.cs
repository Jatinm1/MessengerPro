// ============================================================
// ChatApp.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs
// NEW FILE — VULN-003 / VULN-024
// ============================================================
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Domain.Entities;
using ChatApp.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace ChatApp.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly DapperContext _ctx;
    public RefreshTokenRepository(DapperContext ctx) => _ctx = ctx;

    // ── Family ───────────────────────────────────────────────

    public async Task CreateFamilyAsync(RefreshTokenFamily f)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_CreateRefreshTokenFamily",
            new { f.FamilyId, f.UserId, f.DeviceId, f.DeviceName },
            commandType: CommandType.StoredProcedure);
    }

    public async Task RevokeFamilyAsync(Guid familyId, string reason)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_RevokeFamily",
            new { FamilyId = familyId, RevokedReason = reason },
            commandType: CommandType.StoredProcedure);
    }

    public async Task RevokeAllUserFamiliesAsync(Guid userId, string reason)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_RevokeAllUserFamilies",
            new { UserId = userId, RevokedReason = reason },
            commandType: CommandType.StoredProcedure);
    }

    // ── Tokens ───────────────────────────────────────────────

    public async Task SaveTokenAsync(RefreshToken t)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_SaveRefreshToken",
            new { t.TokenId, t.FamilyId, t.UserId, t.TokenHash, t.ExpiresAtUtc, t.IpAddress, t.UserAgent },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<RefreshToken>(
            "sp_GetRefreshToken",
            new { TokenHash = tokenHash },
            commandType: CommandType.StoredProcedure);
    }

    public async Task RotateAsync(Guid oldTokenId, RefreshToken newToken)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_RotateRefreshToken",
            new
            {
                OldTokenId = oldTokenId,
                NewTokenId = newToken.TokenId,
                newToken.FamilyId,
                newToken.UserId,
                NewTokenHash = newToken.TokenHash,
                NewExpiresAtUtc = newToken.ExpiresAtUtc,
                newToken.IpAddress,
                newToken.UserAgent
            },
            commandType: CommandType.StoredProcedure);
    }

    // ── Sessions ─────────────────────────────────────────────

    public async Task UpsertSessionAsync(UserSession s)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_UpsertSession",
            new { s.SessionId, s.UserId, s.FamilyId, s.DeviceId, s.DeviceName, s.IpAddress, s.UserAgent, s.ExpiresAtUtc },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<UserSession>(
            "sp_GetActiveSessions",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task TouchSessionAsync(Guid familyId)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_TouchSession",
            new { FamilyId = familyId },
            commandType: CommandType.StoredProcedure);
    }

    // ── Lockout ──────────────────────────────────────────────

    public async Task RecordLoginAttemptAsync(string userName, string ipAddress, bool succeeded)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_RecordLoginAttempt",
            new { UserName = userName, IpAddress = ipAddress, Succeeded = succeeded },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> GetRecentFailedAttemptsAsync(string userName, int windowMinutes)
    {
        using var con = _ctx.CreateConnection();
        return await con.ExecuteScalarAsync<int>(
            "sp_GetRecentFailedAttempts",
            new { UserName = userName, WindowMins = windowMinutes },
            commandType: CommandType.StoredProcedure);
    }

    public async Task CreateLockoutAsync(Guid userId, int durationMinutes)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_CreateLockout",
            new { UserId = userId, DurationMin = durationMinutes },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<AccountLockout?> GetActiveLockoutAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<AccountLockout>(
            "sp_GetActiveLockout",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }
}
