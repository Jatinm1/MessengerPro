using ChatApp.Application.DTOs.User;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Domain.Chat;
using ChatApp.Domain.Entities;
using ChatApp.Domain.ValueObjects;
using ChatApp.Infrastructure.Persistence;
using Dapper;
using System.Data;

public class UserRepository : IUserRepository
{
    private readonly DapperContext _ctx;
    public UserRepository(DapperContext ctx) => _ctx = ctx;

    public async Task<User?> GetByUserNameAsync(string userName)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<User>(
            "sp_GetUserByUserName",
            new { UserName = userName },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<User?> GetByIdAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<User>(
            "sp_GetUserById",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<Guid> CreateAsync(string userName, string displayName, string passwordHash)
    {
        using var con = _ctx.CreateConnection();
        var id = Guid.NewGuid();

        await con.ExecuteAsync(
            "sp_CreateUser",
            new { UserId = id, UserName = userName, DisplayName = displayName, PasswordHash = passwordHash },
            commandType: CommandType.StoredProcedure);

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

    public async Task<ChatApp.Application.DTOs.User.OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<ChatApp.Application.DTOs.User.OtherUserProfileDto>(
            "sp_GetUserProfileById",
            new { UserId = userId, ViewerId = viewerId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateUserProfileAsync(Guid userId, string? displayName, string? bio)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_UpdateUserProfile",
            new { UserId = userId, DisplayName = displayName, Bio = bio },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateProfilePhotoAsync(Guid userId, string profilePhotoUrl)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_UpdateProfilePhoto",
            new { UserId = userId, ProfilePhotoUrl = profilePhotoUrl },
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
