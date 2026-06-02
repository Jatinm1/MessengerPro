using ChatApp.Application.DTOs.Auth;
using ChatApp.Application.DTOs.User;
using ChatApp.Domain.Entities;
using ChatApp.Domain.ValueObjects;

namespace ChatApp.Application.Interfaces.IRepositories;

public interface IUserRepository
{
    Task<User?> GetByUserNameAsync(string userName);
    Task<User?> GetByIdAsync(Guid userId);
    Task<Guid> CreateAsync(string userName, string displayName, string passwordHash, string emailId);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<DTOs.User.OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId);
    Task UpdateUserProfileAsync(Guid userId, string? displayName, string? bio);
    Task UpdateProfilePhotoAsync(Guid userId, string profilePhotoUrl);
    Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline);
    Task LogoutUserAsync(Guid userId);
    // IUserRepository.cs — add these two method signatures

    Task SavePublicKeyAsync(Guid userId, string publicKeyJwk);
    Task<IEnumerable<UserPublicKeyDto>> GetPublicKeysAsync(List<Guid> userIds);
    Task SaveKeyBackupAsync(Guid userId, string encryptedKeyBackup, string salt);
    Task<KeyBackupDto?> GetKeyBackupAsync(Guid userId);
    Task SaveDeviceSwitchPinAsync(Guid userId, string hashedPin, DateTime expiresAt);
    Task<DeviceSwitchPinDto?> GetDeviceSwitchPinAsync(Guid userId);
    Task ClearDeviceSwitchPinAsync(Guid userId);
}