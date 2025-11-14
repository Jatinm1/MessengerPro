using ChatApp.Domain.Chat;
using ChatApp.Domain.Users;
using Dapper;
using System.Data;

namespace ChatApp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DapperContext _ctx;
    public UserRepository(DapperContext ctx) => _ctx = ctx;

    public async Task<User?> GetByUserNameAsync(string userName)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<User>(
            "SELECT UserId, UserName, DisplayName, PasswordHash, CreatedAtUtc FROM Users WHERE UserName=@user",
            new { user = userName });
    }

    public async Task<User?> GetByIdAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<User>(
            "SELECT UserId, UserName, DisplayName, PasswordHash, CreatedAtUtc FROM Users WHERE UserId=@id",
            new { id = userId });
    }

    public async Task<Guid> CreateAsync(string userName, string displayName, string passwordHash)
    {
        using var con = _ctx.CreateConnection();
        var id = Guid.NewGuid();
        await con.ExecuteAsync(
            "INSERT INTO Users(UserId,UserName,DisplayName,PasswordHash) VALUES(@id,@u,@d,@p)",
            new { id, u = userName, d = displayName, p = passwordHash });
        return id;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<UserDto>(
            "sp_GetAllUsers",
            commandType: CommandType.StoredProcedure);
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<UserProfileDto>(
            "sp_GetUserProfile",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<OtherUserProfileDto>(
            "sp_GetUserProfileById",
            new { UserId = userId, ViewerId = viewerId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateUserProfileAsync(Guid userId, string? displayName, string? profilePhotoUrl, string? bio)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_UpdateUserProfile",
            new { UserId = userId, DisplayName = displayName, ProfilePhotoUrl = profilePhotoUrl, Bio = bio },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_UpdateUserOnlineStatus",
            new { UserId = userId, IsOnline = isOnline },
            commandType: CommandType.StoredProcedure);
    }
    public async Task LogoutUserAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_LogoutUser",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

}