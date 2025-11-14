namespace ChatApp.Domain.Users;
public record User
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string PasswordHash { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
}
