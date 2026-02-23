// ========================================
// ChatApp.Api/Program.cs
// ========================================
using ChatApp.Api.Hubs;
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

// ========================================
// Railway Dynamic Port Binding
// ========================================
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}
var cfg = builder.Configuration;

// ========================================
// CORS Configuration
// ========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
    "https://messenger-pro-front.vercel.app"
)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ========================================
// File Upload Configuration
// ========================================
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52428800; // 50MB
});

// ========================================
// Database Context
// ========================================
builder.Services.AddSingleton<DapperContext>();

// ========================================
// Repository Layer (Data Access)
// ========================================
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IFriendRepository, FriendRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();

// ========================================
// Application Services (Business Logic)
// ========================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ========================================
// Infrastructure Services (External)
// ========================================
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// ========================================
// SignalR
// ========================================
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // For development debugging
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// ========================================
// JWT Authentication
// ========================================
var jwtKey = cfg["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        // ✅ Allow SignalR to receive token via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ========================================
// Controllers & API Explorer
// ========================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ========================================
// Swagger Configuration
// ========================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ChatApp API",
        Version = "v1",
        Description = "Full-Featured Chat Application API with SignalR and JWT Authentication",
        Contact = new OpenApiContact
        {
            Name = "Your Name",
            Email = "your.email@example.com"
        }
    });

    // JWT Authentication in Swagger
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer' [space] and then your JWT token.\n\nExample: Bearer eyJhbGc...",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
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
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Optional: Add XML comments if you have them
    // var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    // c.IncludeXmlComments(xmlPath);
});

// ========================================
// Build Application
// ========================================
var app = builder.Build();

// ========================================
// Middleware Pipeline
// ========================================

// CORS - Must be before Authentication
app.UseCors("AllowAll");

// Swagger UI (available in all environments)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatApp API v1");
    c.RoutePrefix = string.Empty; // Opens Swagger at root URL
    c.DocumentTitle = "ChatApp API Documentation";
});

// HTTPS Redirection
//if (!app.Environment.IsDevelopment())
//{
//    app.UseHttpsRedirection();
//}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Map SignalR Hub
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

// ========================================
// Application Info (Optional)
// ========================================
app.MapGet("/", () => new
{
    application = "ChatApp API",
    version = "1.0.0",
    status = "Running",
    timestamp = DateTime.UtcNow,
    documentation = "/swagger",
    signalr = "/hubs/chat"
}).ExcludeFromDescription();

// ========================================
// Health Check (Optional but recommended)
// ========================================
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow
})).ExcludeFromDescription();

// ========================================
// Start Application
// ========================================
Console.WriteLine("🚀 ChatApp API Starting...");
Console.WriteLine($"📝 Swagger UI: {app.Urls.FirstOrDefault() ?? "http://localhost:5000"}");
Console.WriteLine($"🔌 SignalR Hub: {app.Urls.FirstOrDefault() ?? "http://localhost:5000"}/hubs/chat");

app.Run();

Console.WriteLine("✅ ChatApp API is running successfully!");