using ChatApp.Application.DTOs.User;
using ChatApp.Domain.ValueObjects;

public interface IUserService
{
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<ChatApp.Application.DTOs.User.OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId);
    //Task UpdateUserProfileAsync(Guid userId, string? displayName, string? profilePhotoUrl, string? bio);
    Task UpdateProfilePhotoAsync(Guid userId, string profilePhotoUrl);
    Task UpdateUserProfileAsync(Guid userId, string? displayName, string? bio);
    Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline);
}