using ChatApp.Application.DTOs.Auth;
using ChatApp.Application.DTOs.User;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Domain.ValueObjects;

namespace ChatApp.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;

    public UserService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
        => await _users.GetUserProfileAsync(userId);

    public async Task<DTOs.User.OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId)
        => await _users.GetUserProfileByIdAsync(userId, viewerId);

    // Updated - removed profilePhotoUrl parameter
    public async Task UpdateUserProfileAsync(Guid userId, string? displayName, string? bio)
        => await _users.UpdateUserProfileAsync(userId, displayName, bio);

    // New: update only photo
    public async Task UpdateProfilePhotoAsync(Guid userId, string profilePhotoUrl)
        => await _users.UpdateProfilePhotoAsync(userId, profilePhotoUrl);

    public async Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline)
        => await _users.UpdateUserOnlineStatusAsync(userId, isOnline);

    // UserService.cs — implement the two methods

    public async Task SavePublicKeyAsync(Guid userId, string publicKeyJwk)
    {
        await _users.SavePublicKeyAsync(userId, publicKeyJwk);
    }

    public async Task<IEnumerable<UserPublicKeyDto>> GetPublicKeysAsync(List<Guid> userIds)
    {
        return await _users.GetPublicKeysAsync(userIds);
    }
    public async Task SaveKeyBackupAsync(Guid userId, string encryptedKeyBackup, string salt)
    => await _users.SaveKeyBackupAsync(userId, encryptedKeyBackup, salt);

    public async Task<KeyBackupDto?> GetKeyBackupAsync(Guid userId)
        => await _users.GetKeyBackupAsync(userId);

    public async Task SaveDeviceSwitchPinAsync(Guid userId, string hashedPin, DateTime expiresAt)
    => await _users.SaveDeviceSwitchPinAsync(userId, hashedPin, expiresAt);

    public async Task<(bool Success, string? Error)> VerifyDeviceSwitchPinAsync(Guid userId, string pin)
    {
        var stored = await _users.GetDeviceSwitchPinAsync(userId);
        if (stored == null)
            return (false, "No PIN request found. Please request a new PIN.");

        if (stored.ExpiresAt < DateTime.UtcNow)
            return (false, "PIN has expired. Please request a new one.");

        if (!BCrypt.Net.BCrypt.Verify(pin, stored.HashedPin))
            return (false, "Incorrect PIN.");

        // Clear pin after successful verify — one-time use
        await _users.ClearDeviceSwitchPinAsync(userId);
        return (true, null);
    }
}
