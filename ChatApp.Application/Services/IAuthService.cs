// ChatApp.Application/Services/IAuthService.cs
using ChatApp.Domain.Users;
namespace ChatApp.Application.Services
{
    public interface IAuthService
    {
        Task<(string token, User user)> LoginAsync(string userName, string password);
        Task<User> RegisterAsync(string userName, string displayName, string password);
        Task LogoutAsync(Guid userId);

    }
}


