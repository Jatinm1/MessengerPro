namespace ChatApp.Domain.Entities;
public record User
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string PasswordHash { get; init; } = default!;
    public string? ProfilePhotoUrl { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class LoginResponse
{
    public string Token { get; set; }
    public User User { get; set; }
}
