using ChatApp.Domain.Users;

public interface IUserService
{
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId);
    Task UpdateUserProfileAsync(Guid userId, string? displayName, string? profilePhotoUrl, string? bio);
    Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline);
}