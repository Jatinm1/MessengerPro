using Microsoft.AspNetCore.Http;

namespace ChatApp.Api.Helpers;

public static class CookieHelper
{
    public static void SetAuthCookies(
        HttpResponse response,
        bool secure,
        string accessToken,
        string refreshToken,
        Guid deviceId)
    {
        response.Cookies.Append("access_token", accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

        response.Cookies.Append("refresh_token", refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth/refresh",
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

        response.Cookies.Append("device_id", deviceId.ToString(),
            new CookieOptions
            {
                HttpOnly = false,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
    }

    public static void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Append("access_token", "", new CookieOptions
        {
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/"
        });

        response.Cookies.Append("refresh_token", "", new CookieOptions
        {
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/api/auth/refresh"
        });

        response.Cookies.Append("device_id", "", new CookieOptions
        {
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/"
        });
    }
}