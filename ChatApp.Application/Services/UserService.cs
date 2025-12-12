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
}
