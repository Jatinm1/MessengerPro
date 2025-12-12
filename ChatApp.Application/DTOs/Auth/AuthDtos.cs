using ChatApp.Application.DTOs.User;

namespace ChatApp.Application.DTOs.Auth;

public record LoginRequest(string UserName, string Password);
public record RegisterRequest(string UserName, string DisplayName, string Password);
public record AuthResponse(string Token, UserDto User);