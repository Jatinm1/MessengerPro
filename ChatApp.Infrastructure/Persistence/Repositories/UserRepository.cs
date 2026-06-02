using ChatApp.Application.DTOs.Auth;
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

    public async Task<Guid> CreateAsync(string userName, string displayName, string passwordHash, string emailId)
    {
        using var con = _ctx.CreateConnection();
        var id = Guid.NewGuid();

        await con.ExecuteAsync(
            "sp_CreateUser",
            new { UserId = id, UserName = userName, DisplayName = displayName, PasswordHash = passwordHash, Email = emailId },
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
    // UserRepository.cs — implement using Dapper

    public async Task SavePublicKeyAsync(Guid userId, string publicKeyJwk)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            @"UPDATE Users 
          SET PublicKey = @PublicKey 
          WHERE UserId = @UserId",
            new { UserId = userId, PublicKey = publicKeyJwk });
    }

    public async Task<IEnumerable<UserPublicKeyDto>> GetPublicKeysAsync(List<Guid> userIds)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<UserPublicKeyDto>(
            @"SELECT CAST(UserId AS NVARCHAR(36)) AS UserId, 
                 PublicKey AS PublicKeyJwk
          FROM Users
          WHERE UserId IN @UserIds
            AND PublicKey IS NOT NULL",
            new { UserIds = userIds });
    }

    public async Task SaveKeyBackupAsync(Guid userId, string encryptedKeyBackup, string salt)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            @"UPDATE Users 
          SET EncryptedKeyBackup = @EncryptedKeyBackup,
              KeyBackupSalt      = @Salt
          WHERE UserId = @UserId",
            new { UserId = userId, EncryptedKeyBackup = encryptedKeyBackup, Salt = salt });
    }

    public async Task<KeyBackupDto?> GetKeyBackupAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<KeyBackupDto>(
            @"SELECT EncryptedKeyBackup, KeyBackupSalt AS Salt
          FROM Users WHERE UserId = @UserId
          AND EncryptedKeyBackup IS NOT NULL",
            new { UserId = userId });
    }

    public async Task SaveDeviceSwitchPinAsync(Guid userId, string hashedPin, DateTime expiresAt)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            @"UPDATE Users SET DeviceSwitchPin = @Pin, DeviceSwitchPinExpiresAt = @Expires
          WHERE UserId = @UserId",
            new { UserId = userId, Pin = hashedPin, Expires = expiresAt });
    }

    public async Task<DeviceSwitchPinDto?> GetDeviceSwitchPinAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<DeviceSwitchPinDto>(
            @"SELECT DeviceSwitchPin AS HashedPin, DeviceSwitchPinExpiresAt AS ExpiresAt
          FROM Users WHERE UserId = @UserId",
            new { UserId = userId });
    }

    public async Task ClearDeviceSwitchPinAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            @"UPDATE Users SET DeviceSwitchPin = NULL, DeviceSwitchPinExpiresAt = NULL
          WHERE UserId = @UserId",
            new { UserId = userId });
    }



}
