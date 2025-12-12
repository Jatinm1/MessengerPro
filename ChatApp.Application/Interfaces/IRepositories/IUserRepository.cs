using ChatApp.Application.DTOs.User;
using ChatApp.Domain.Entities;
using ChatApp.Domain.ValueObjects;

namespace ChatApp.Application.Interfaces.IRepositories;

public interface IUserRepository
{
    Task<User?> GetByUserNameAsync(string userName);
    Task<User?> GetByIdAsync(Guid userId);
    Task<Guid> CreateAsync(string userName, string displayName, string passwordHash);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<DTOs.User.OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId);
    Task UpdateUserProfileAsync(Guid userId, string? displayName, string? bio);
    Task UpdateProfilePhotoAsync(Guid userId, string profilePhotoUrl);
    Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline);
    Task LogoutUserAsync(Guid userId);
}