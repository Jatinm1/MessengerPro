// ChatApp.Infrastructure/Repositories/IUserRepository.cs
using ChatApp.Domain.Chat;
using ChatApp.Domain.Users;
namespace ChatApp.Infrastructure.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByUserNameAsync(string userName);
        Task<User?> GetByIdAsync(Guid userId);
        Task<Guid> CreateAsync(string userName, string displayName, string passwordHash);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();

        // Profile methods
        Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
        Task<OtherUserProfileDto?> GetUserProfileByIdAsync(Guid userId, Guid viewerId);
        //Task UpdateUserProfileAsync(Guid userId, string? displayName, string? profilePhotoUrl, string? bio);
        Task UpdateProfilePhotoAsync(Guid userId, string profilePhotoUrl);

        Task UpdateUserProfileAsync(Guid userId, string? displayName, string? bio);
        Task UpdateUserOnlineStatusAsync(Guid userId, bool isOnline);
        Task LogoutUserAsync(Guid userId);

    }
}
