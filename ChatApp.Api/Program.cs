using ChatApp.Api.SignalR;
using ChatApp.Application.Services;
using ChatApp.Infrastructure;
using ChatApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// =============================
// ✅ CORS for Angular frontend
// =============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true); // allows localhost, file://, etc.
    });
});

// =============================
// ✅ SignalR setup
// =============================
builder.Services.AddSignalR();

// =============================
// ✅ Dependency Injection (DI)
// =============================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserService, UserService>(); // NEW
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IFriendRepository, FriendRepository>();
builder.Services.AddScoped<IFriendService, FriendService>();

builder.Services.AddSingleton<DapperContext>(); // holds SqlConnection factory

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// =============================
// ✅ Swagger Configuration (with JWT)
// =============================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ChatApp API",
        Version = "v1",
        Description = "Full-fledged Chat Application API with SignalR and JWT Auth"
    });

    // ✅ JWT Authentication Configuration
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste your JWT token below (Bearer prefix added automatically).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,  // 👈 use ApiKey instead of Http
        Scheme = "Bearer",
        BearerFormat = "JWT"
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
            new string[] {}
        }
    });
});


// =============================
// ✅ JWT Authentication setup
// =============================
var key = Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        };

        // ✅ Allow SignalR to receive token via query string (?access_token=)
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// =============================
// ✅ Build and Configure Pipeline
// =============================
var app = builder.Build();

app.UseCors("AllowAll");

// Swagger UI setup
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatApp API v1");
    c.RoutePrefix = string.Empty; // opens Swagger directly at root URL
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat"); // SignalR endpoint

app.Run();
