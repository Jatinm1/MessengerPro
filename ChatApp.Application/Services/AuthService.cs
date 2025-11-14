// ChatApp.Application/Services/AuthService.cs
using ChatApp.Infrastructure.Repositories;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ChatApp.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly IConfiguration _cfg;
        public AuthService(IUserRepository users, IConfiguration cfg) { _users = users; _cfg = cfg; }

        public async Task<(string, ChatApp.Domain.Users.User)> LoginAsync(string userName, string password)
        {
            var user = await _users.GetByUserNameAsync(userName) ?? throw new UnauthorizedAccessException("Invalid user");
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) throw new UnauthorizedAccessException("Invalid password");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim("uname", user.UserName)
        };
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);
            return (new JwtSecurityTokenHandler().WriteToken(token), user);
        }

        public async Task<ChatApp.Domain.Users.User> RegisterAsync(string userName, string displayName, string password)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
            var id = await _users.CreateAsync(userName, displayName, hash);
            return (await _users.GetByIdAsync(id))!;
        }
        public async Task LogoutAsync(Guid userId)
        {
            await _users.UpdateUserOnlineStatusAsync(userId, false);
            await _users.LogoutUserAsync(userId);
        }

    }

}