// ============================================================
// ChatApp.Api/Middleware/CsrfMiddleware.cs
// NEW FILE — Fixes:
//   VULN-018: CSRF protection via Double Submit Cookie pattern.
//
// HOW IT WORKS:
//   1. On every request, if no XSRF-TOKEN cookie exists, the
//      middleware generates a cryptographically random token and
//      sets it as a non-HttpOnly cookie so Angular can read it.
//   2. For all state-changing requests (POST/PUT/PATCH/DELETE),
//      the middleware requires an X-XSRF-TOKEN request header
//      containing the same value as the XSRF-TOKEN cookie.
//   3. Since a cross-origin attacker cannot read the cookie
//      (SameSite + CORS), they cannot set the matching header.
//
// EXEMPT paths:
//   - GET, HEAD, OPTIONS (safe methods per RFC 7231)
//   - /api/auth/login    (pre-auth, no cookie yet)
//   - /api/auth/register (pre-auth, no cookie yet)
//   - /hubs/*            (SignalR — token auth via cookie)
//   - /health
// ============================================================
using System.Security.Cryptography;

namespace ChatApp.Api.Middleware;

public sealed class CsrfMiddleware
{
    private const string CookieName = "XSRF-TOKEN";
    private const string HeaderName = "X-XSRF-TOKEN";
    private const int TokenBytes = 32;              // 256-bit

    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase)
        { "GET", "HEAD", "OPTIONS", "TRACE" };

    private static readonly string[] ExemptPrefixes =
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/signalr-token",   // issued right after login before XSRF cookie is set
        "/hubs/",
        "/health"
    };

    private readonly RequestDelegate _next;
    private readonly bool _isProduction;

    public CsrfMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _isProduction = env.IsProduction();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        // ── 1. Ensure the XSRF-TOKEN cookie exists ────────────
        var existingToken = context.Request.Cookies[CookieName];
        var token = string.IsNullOrWhiteSpace(existingToken)
            ? GenerateToken()
            : existingToken;

        if (string.IsNullOrWhiteSpace(existingToken))
        {
            // Issue new token — non-HttpOnly so Angular JS can read it
            context.Response.Cookies.Append(CookieName, token, new CookieOptions
            {
                HttpOnly = false,                          // Angular must read this
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });
        }

        // ── 2. Validate on state-changing requests ────────────
        if (!SafeMethods.Contains(method) && !IsExempt(path))
        {
            var headerToken = context.Request.Headers[HeaderName].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(headerToken) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(token.PadRight(TokenBytes)),
                    System.Text.Encoding.UTF8.GetBytes(headerToken.PadRight(TokenBytes))))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"message\":\"CSRF token missing or invalid.\"}");
                return;
            }
        }

        await _next(context);
    }

    // ── Helpers ───────────────────────────────────────────────

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes));

    private static bool IsExempt(string path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

// ── Extension method ──────────────────────────────────────────

public static class CsrfMiddlewareExtensions
{
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app)
        => app.UseMiddleware<CsrfMiddleware>();
}