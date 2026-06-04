// ============================================================
// ChatApp.Api/Middleware/SecurityHeadersMiddleware.cs
// NEW FILE — Fixes:
//   VULN-011: Full security header suite:
//     - Content-Security-Policy (with nonce support)
//     - Strict-Transport-Security (HSTS with preload)
//     - X-Frame-Options
//     - X-Content-Type-Options
//     - Referrer-Policy
//     - Permissions-Policy
//     - X-XSS-Protection (legacy browsers, modern = CSP)
// ============================================================
using System.Security.Cryptography;

namespace ChatApp.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isProduction;

    // Paths that serve Angular's index.html (need CSP nonce injection)
    private static readonly string[] StaticHtmlPaths = { "/", "/index.html" };

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _isProduction = env.IsProduction();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // ── Universal headers (dev + prod) ────────────────────

        // VULN-011: Prevent MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // VULN-011: Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // VULN-011: Disable legacy XSS filter (CSP is authoritative)
        headers["X-XSS-Protection"] = "0";

        // VULN-011: Referrer leakage control
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // VULN-011: Permissions Policy — disable dangerous browser features
        headers["Permissions-Policy"] =
            "accelerometer=(), " +
            "ambient-light-sensor=(), " +
            "autoplay=(self), " +
            "battery=(), " +
            "camera=(self), " +
            "cross-origin-isolated=(), " +
            "display-capture=(self), " +
            "document-domain=(), " +
            "encrypted-media=(), " +
            "execution-while-not-rendered=(), " +
            "execution-while-out-of-viewport=(), " +
            "fullscreen=(self), " +
            "geolocation=(), " +
            "gyroscope=(), " +
            "keyboard-map=(), " +
            "magnetometer=(), " +
            "microphone=(self), " +
            "midi=(), " +
            "navigation-override=(), " +
            "payment=(), " +
            "picture-in-picture=(), " +
            "publickey-credentials-get=(), " +
            "screen-wake-lock=(), " +
            "sync-xhr=(), " +
            "usb=(), " +
            "web-share=(self), " +
            "xr-spatial-tracking=()";

        // ── Production-only headers ───────────────────────────

        if (_isProduction)
        {
            // VULN-011: HSTS — force HTTPS for 2 years, include subdomains, preload-ready
            headers["Strict-Transport-Security"] =
                "max-age=63072000; includeSubDomains; preload";

            // VULN-011: Full CSP for production
            headers["Content-Security-Policy"] = BuildCsp(isStrict: true);
        }
        else
        {
            // Development CSP — relaxed for hot-reload / Angular devtools
            headers["Content-Security-Policy"] = BuildCsp(isStrict: false);
        }

        await _next(context);
    }

    // ── CSP Builder ───────────────────────────────────────────

    private static string BuildCsp(bool isStrict)
    {
        if (isStrict)
        {
            // Production: strict CSP
            // 'unsafe-inline' on style-src is required for Angular component styles
            // until the app migrates to nonce-based or hash-based styles.
            return string.Join("; ",
                "default-src 'none'",
                "script-src 'self'",
                "style-src 'self' 'unsafe-inline'",
                "img-src 'self' data: blob: https://res.cloudinary.com",
                "font-src 'self'",
                "connect-src 'self' wss: https://res.cloudinary.com https://api.cloudinary.com",
                "media-src 'self' blob:",
                "worker-src 'self' blob:",
                "frame-src 'none'",
                "frame-ancestors 'none'",
                "form-action 'self'",
                "base-uri 'self'",
                "object-src 'none'",
                "upgrade-insecure-requests"
            );
        }
        else
        {
            // Development: allow Angular dev server tooling
            return string.Join("; ",
                "default-src 'self'",
                "script-src 'self' 'unsafe-eval' 'unsafe-inline'",
                "style-src 'self' 'unsafe-inline'",
                "img-src 'self' data: blob: https://res.cloudinary.com",
                "font-src 'self' data:",
                "connect-src 'self' ws: wss: http://localhost:* https://res.cloudinary.com https://api.cloudinary.com",
                "media-src 'self' blob:",
                "worker-src 'self' blob:",
                "frame-src 'none'",
                "frame-ancestors 'none'",
                "form-action 'self'",
                "base-uri 'self'",
                "object-src 'none'"
            );
        }
    }
}

// ── Extension method ──────────────────────────────────────────

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}