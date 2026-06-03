namespace ChatApp.Domain.Entities;

public record User
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string PasswordHash { get; init; } = default!;  // Never serialized — see UserDto
    public string? ProfilePhotoUrl { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record UserDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    DateTime CreatedAtUtc);

/// <summary>
/// VULN-022 fix: LoginResponse uses UserDto (no PasswordHash) instead of User entity.
/// </summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public Guid DeviceId { get; set; }
    public UserDto User { get; set; } = default!;
}