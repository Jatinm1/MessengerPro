using ChatApp.Application.DTOs.Auth;
using ChatApp.Domain.Entities;

namespace ChatApp.Application.Interfaces.IServices;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string userName, string password);
    Task<User> RegisterAsync(string userName, string displayName, string password);
    Task LogoutAsync(Guid userId);
}