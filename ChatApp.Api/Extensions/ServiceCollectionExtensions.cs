// ============================================================
// ChatApp.Api/Extensions/ServiceCollectionExtensions.cs
// MODIFIED FILE — registers IRefreshTokenRepository + fixes
//   VULN-002, VULN-025
// ============================================================
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using ChatApp.Application.Services;
using ChatApp.Infrastructure.Persistence;
using ChatApp.Infrastructure.Persistence.Repositories;
using ChatApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ChatApp.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DapperContext>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IFriendRepository, FriendRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();  // NEW
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }

    /// <summary>
    /// Configures JWT Bearer auth.
    /// VULN-002: ValidateIssuer + ValidateAudience = true
    /// VULN-025: Key length enforced before use
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key not configured.");
        // VULN-025
        if (jwtKey.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 256 bits (32 characters).");

        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        var sigKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = sigKey,
                ValidateIssuer = true,     // VULN-002
                ValidIssuer = issuer,
                ValidateAudience = true,     // VULN-002
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // VULN-005: Read token from HttpOnly cookie
                    var cookieToken = context.HttpContext.Request.Cookies["access_token"];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                        return Task.CompletedTask;
                    }

                    // SignalR fallback via query string (negotiation only)
                    if (context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        var qs = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(qs))
                            context.Token = qs;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}
