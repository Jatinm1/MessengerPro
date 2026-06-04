// ChatApp.Api/Helpers/CookieHelper.cs
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
        // SameSite.None required for cross-origin requests (Angular on :4200 → API on :7006).
        // SameSite.None mandates Secure=true; on HTTP dev environments set secure=false and use Lax.
        var sameSite = secure ? SameSiteMode.None : SameSiteMode.Lax;

        response.Cookies.Append("access_token", accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

        response.Cookies.Append("refresh_token", refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                // Use /api/auth so both /api/auth/refresh AND /api/auth/signalr-token receive it.
                Path = "/api/auth",
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

        response.Cookies.Append("device_id", deviceId.ToString(),
            new CookieOptions
            {
                HttpOnly = false,
                Secure = secure,
                SameSite = sameSite,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
    }

    public static void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Append("access_token", "", new CookieOptions
        {
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/",
            SameSite = SameSiteMode.None,
            Secure = true
        });

        response.Cookies.Append("refresh_token", "", new CookieOptions
        {
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/api/auth",
            SameSite = SameSiteMode.None,
            Secure = true
        });

        response.Cookies.Append("device_id", "", new CookieOptions
        {
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/",
            SameSite = SameSiteMode.None,
            Secure = true
        });
    }
}