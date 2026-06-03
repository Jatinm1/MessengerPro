using ChatApp.Api.Helpers;
using ChatApp.Application.Interfaces.IServices;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace ChatApp.Api.Middleware;

public sealed class TokenRefreshMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] SkippedPaths =
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh"
    };

    public TokenRefreshMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAuthService authService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (SkippedPaths.Any(p =>
                path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var accessToken = context.Request.Cookies["access_token"];
        var refreshToken = context.Request.Cookies["refresh_token"];
        var deviceIdStr = context.Request.Cookies["device_id"];

        if (!string.IsNullOrWhiteSpace(accessToken)
            && !string.IsNullOrWhiteSpace(refreshToken)
            && Guid.TryParse(deviceIdStr, out var deviceId)
            && IsNearExpiry(accessToken))
        {
            try
            {
                var ip =
                    context.Connection.RemoteIpAddress?.ToString();

                var userAgent =
                    context.Request.Headers.UserAgent.ToString();

                var result = await authService.RefreshAsync(
                    refreshToken,
                    deviceId,
                    ip,
                    userAgent);

                CookieHelper.SetAuthCookies(
                    context.Response,
                    context.Request.IsHttps,
                    result.AccessToken,
                    result.RefreshToken,
                    result.DeviceId);

                context.Request.Headers.Authorization =
                    $"Bearer {result.AccessToken}";
            }
            catch (SecurityTokenException)
            {
                // Invalid refresh token.
                // Request will continue and eventually return 401.
            }
            catch
            {
                // Never block requests due to refresh failures.
            }
        }

        await _next(context);
    }

    private static bool IsNearExpiry(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);

            return token.ValidTo <= DateTime.UtcNow.AddMinutes(2);
        }
        catch
        {
            return true;
        }
    }
}

public static class TokenRefreshMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenRefresh(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<TokenRefreshMiddleware>();
    }
}