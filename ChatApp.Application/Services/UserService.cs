using ChatApp.Domain.Users;
using ChatApp.Infrastructure.Repositories;

namespace ChatApp.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;

    public UserService(IUserRepository users) => _users = users;

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
        => await _users.GetUserProfileAsync(userId);

    public async Task<OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId)
        => await _users.GetUserProfileByIdAsync(userId, viewerId);

    public async Task UpdateUserProfileAsync(Guid userId, string? displayName, string? profilePhotoUrl, string? bio)
        => await _users.UpdateUserProfileAsync(userId, displayName, profilePhotoUrl, bio);

    public async Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline)
        => await _users.UpdateUserOnlineStatusAsync(userId, isOnline);
}