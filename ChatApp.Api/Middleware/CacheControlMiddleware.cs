// ============================================================
// ChatApp.Api/Middleware/CacheControlMiddleware.cs
// NEW FILE — Fixes:
//   VULN-028: Cache-Control strategy to prevent browser back
//             button from restoring protected pages/data after
//             logout and to prevent CDN/proxy caching of
//             authenticated API responses.
//
// STRATEGY:
//   - All /api/* responses: Cache-Control: no-store, no-cache,
//     must-revalidate  +  Pragma: no-cache  +  Expires: 0
//   - Auth endpoints (/api/auth/*): same + additional
//     Clear-Site-Data header on logout.
//   - Static assets (/*, not /api/*): not touched — the Angular
//     build handles cache busting via content-hash filenames.
//   - SignalR /hubs/*: no caching.
// ============================================================

namespace ChatApp.Api.Middleware;

public sealed class CacheControlMiddleware
{
    private readonly RequestDelegate _next;

    // Routes that must never be cached
    private static readonly string[] NoCachePrefixes =
    {
        "/api/",
        "/hubs/"
    };

    // Logout endpoints — get Clear-Site-Data header too
    private static readonly string[] LogoutPaths =
    {
        "/api/auth/logout"
    };

    public CacheControlMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (RequiresNoCache(path))
        {
            // Prevent every layer (browser, CDN, proxy) from caching
            context.Response.Headers["Cache-Control"] =
                "no-store, no-cache, must-revalidate, proxy-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            // VULN-028: On logout — tell the browser to wipe all site data
            // This kills bfcache, sessionStorage, cookies, etc.
            if (IsLogoutPath(path) &&
                (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)))
            {
                // Set after the downstream writes the response
                context.Response.OnStarting(() =>
                {
                    // Clear-Site-Data forces browser to purge its cache for this origin.
                    // "cache" clears HTTP cache + bfcache.
                    // "storage" clears sessionStorage/localStorage (belt-and-suspenders).
                    // "executionContexts" terminates JS execution contexts (prevents back-button JS restore).
                    context.Response.Headers["Clear-Site-Data"] =
                        "\"cache\", \"storage\", \"executionContexts\"";
                    return Task.CompletedTask;
                });
            }
        }

        await _next(context);
    }

    // ── Helpers ───────────────────────────────────────────────

    private static bool RequiresNoCache(string path)
    {
        foreach (var prefix in NoCachePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsLogoutPath(string path)
    {
        foreach (var logoutPath in LogoutPaths)
        {
            if (path.StartsWith(logoutPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

// ── Extension method ──────────────────────────────────────────

public static class CacheControlMiddlewareExtensions
{
    public static IApplicationBuilder UseCacheControl(this IApplicationBuilder app)
        => app.UseMiddleware<CacheControlMiddleware>();
}