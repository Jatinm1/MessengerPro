// ============================================================
// ChatApp.Api/Program.cs
// MODIFIED FILE — Fixes:
//   VULN-002: ValidateIssuer + ValidateAudience = true
//   VULN-003: Cookie-based token extraction for SignalR
//   VULN-005: Tokens set via HttpOnly cookies; Bearer reads from
//             access_token cookie first, then Authorization header
//   VULN-011: Full security headers — CSP, HSTS, X-Frame-Options,
//             X-Content-Type-Options, Referrer-Policy, Permissions-Policy
//   VULN-018: CSRF Double Submit Cookie middleware
//   VULN-025: JWT key minimum 32-char enforced at startup
//   VULN-028: Cache-Control no-store on all authenticated responses
// ============================================================
using ChatApp.Api.Hubs;
using ChatApp.Api.Middleware;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using ChatApp.Application.Services;
using ChatApp.Infrastructure.Persistence;
using ChatApp.Infrastructure.Persistence.Repositories;
using ChatApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Railway Dynamic Port ──────────────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://*:{port}");

var cfg = builder.Configuration;

// ── VULN-025: JWT Key Validation ──────────────────────────────
var jwtKey = cfg["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 256 bits (32 characters).");

var jwtIssuer = cfg["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
var jwtAudience = cfg["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

// ── CORS ──────────────────────────────────────────────────────
var allowedOrigins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("SecurePolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());   // required for cookie-based auth
});

// ── File Upload ───────────────────────────────────────────────
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 52_428_800);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 52_428_800);

// ── Database ──────────────────────────────────────────────────
builder.Services.AddSingleton<DapperContext>();

// ── Repositories ──────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IFriendRepository, FriendRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ICallRepository, CallRepository>();

// ── Services ──────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// ── Memory Cache (for lockout / rate limit) ───────────────────
builder.Services.AddMemoryCache();

// ── SignalR ───────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    // NEVER set EnableDetailedErrors = true in production
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// ── VULN-002: JWT with Issuer + Audience validation ───────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,   // VULN-002 fix
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,   // VULN-002 fix
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;

                // VULN-005: Read access token from HttpOnly cookie first
                var cookieToken = context.HttpContext.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                    return Task.CompletedTask;
                }

                // SignalR: fall back to query string (only for /hubs/* paths)
                if (path.StartsWithSegments("/hubs"))
                {
                    var qsToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(qsToken))
                        context.Token = qsToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Controllers + API Explorer ────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger (dev only) ────────────────────────────────────────
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "ChatApp API", Version = "v1" });
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter 'Bearer {token}'",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        c.AddSecurityDefinition("Bearer", securityScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id   = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}

// ── Build ─────────────────────────────────────────────────────
var app = builder.Build();

// ── CORS must be FIRST — before every middleware that can
//   short-circuit the pipeline (CSRF 403, security headers, etc.)
//   If any middleware returns a response before CORS runs,
//   the response lacks Access-Control-Allow-Origin and the
//   browser reports a CORS error regardless of the real cause.
app.UseCors("SecurePolicy");

// ── VULN-011: Security Headers Middleware ────────────────────
app.UseSecurityHeaders();

// ── VULN-018: CSRF Double Submit Cookie Middleware ────────────
app.UseCsrfProtection();

// ── VULN-028: Cache-Control no-store on authenticated routes ─
app.UseCacheControl();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatApp API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TokenRefreshMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHub<CallHub>("/hubs/call").RequireAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .ExcludeFromDescription();

app.Run();