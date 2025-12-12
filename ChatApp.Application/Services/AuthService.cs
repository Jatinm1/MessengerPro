// ChatApp.Application/Services/AuthService.cs
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using ChatApp.Domain.Entities;
using ChatApp.Application.Interfaces.IServices;
using ChatApp.Application.Interfaces.IRepositories;

namespace ChatApp.Application.Services
{
    /// <summary>
    /// Service responsible for user authentication, registration, and logout operations.
    /// Handles password hashing, JWT token generation, and user session management.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly IConfiguration _cfg;

        /// <summary>
        /// Initializes a new instance of AuthService with user repository and configuration.
        /// </summary>
        public AuthService(IUserRepository users, IConfiguration cfg)
        {
            _users = users;
            _cfg = cfg;
        }

        /// <summary>
        /// Authenticates a user with provided credentials and returns a JWT token.
        /// </summary>
        public async Task<LoginResponse> LoginAsync(string userName, string password)
        {
            var user = await _users.GetByUserNameAsync(userName)
                       ?? throw new UnauthorizedAccessException("Invalid user");

            // Verify password against stored hash
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid password");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Create JWT claims
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim("uname", user.UserName)
            };

            // Generate JWT token with 8-hour expiry
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new LoginResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                User = user
            };
        }

        /// <summary>
        /// Registers a new user with hashed password and returns the created user.
        /// </summary>
        public async Task<User> RegisterAsync(string userName, string displayName, string password)
        {
            // Hash password with work factor of 11 (balanced security/performance)
            var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
            var id = await _users.CreateAsync(userName, displayName, hash);
            return (await _users.GetByIdAsync(id))!;
        }

        /// <summary>
        /// Logs out a user by updating their online status and clearing session data.
        /// </summary>
        public async Task LogoutAsync(Guid userId)
        {
            await _users.UpdateUserOnlineStatusAsync(userId, false);
            await _users.LogoutUserAsync(userId);
        }
    }
}