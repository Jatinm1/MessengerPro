// ============================================================
// ChatApp.Api/Helpers/CookieHelper.cs
// MODIFIED FILE — Fixes:
//   VULN-005: All auth cookies are HttpOnly; only device_id is
//             non-HttpOnly (needed by Angular to detect device).
//   VULN-033: ClearAuthCookies uses IDENTICAL attributes to the
//             original Set-Cookie directives (path, HttpOnly,
//             Secure, SameSite) — mismatched attributes leave
//             stale cookies that are never actually deleted.
// ============================================================
using Microsoft.AspNetCore.Http;

namespace ChatApp.Api.Helpers;

public static class CookieHelper
{
    // ── Cookie names ──────────────────────────────────────────
    public const string AccessTokenCookie = "access_token";
    public const string RefreshTokenCookie = "refresh_token";
    public const string DeviceIdCookie = "device_id";

    // ── Paths must match exactly between Set and Delete ───────
    private const string RootPath = "/";
    private const string RefreshTokenPath = "/api/auth/refresh";

    // ── Set all auth cookies on login / refresh ───────────────
    public static void SetAuthCookies(
        HttpResponse response,
        bool secure,
        string accessToken,
        string refreshToken,
        Guid deviceId)
    {
        // VULN-005: HttpOnly = true — JS cannot read access_token
        response.Cookies.Append(AccessTokenCookie, accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.None,
                Path = RootPath,
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

        // VULN-005: HttpOnly = true — JS cannot read refresh_token
        // Scoped to refresh endpoint only — limits exposure surface
        response.Cookies.Append(RefreshTokenCookie, refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.None,
                Path = RefreshTokenPath,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

        // device_id: non-HttpOnly because Angular reads it to display session info
        response.Cookies.Append(DeviceIdCookie, deviceId.ToString(),
            new CookieOptions
            {
                HttpOnly = false,
                Secure = secure,
                SameSite = SameSiteMode.None,
                Path = RootPath,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
    }

    // ── Clear all auth cookies on logout ──────────────────────
    // VULN-033: Every attribute (Path, HttpOnly, Secure, SameSite)
    //           MUST match the original Set-Cookie exactly.
    //           A mismatched path means the browser treats it as a
    //           different cookie and never deletes the original.
    public static void ClearAuthCookies(HttpResponse response)
    {
        // access_token — matches SetAuthCookies: HttpOnly=true, Path=/
        response.Cookies.Append(AccessTokenCookie, string.Empty,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = RootPath,
                Expires = DateTimeOffset.UnixEpoch,
                MaxAge = TimeSpan.Zero
            });

        // refresh_token — matches SetAuthCookies: HttpOnly=true, Path=/api/auth/refresh
        response.Cookies.Append(RefreshTokenCookie, string.Empty,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = RefreshTokenPath,
                Expires = DateTimeOffset.UnixEpoch,
                MaxAge = TimeSpan.Zero
            });

        // device_id — matches SetAuthCookies: HttpOnly=false, Path=/
        response.Cookies.Append(DeviceIdCookie, string.Empty,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = RootPath,
                Expires = DateTimeOffset.UnixEpoch,
                MaxAge = TimeSpan.Zero
            });
    }
}