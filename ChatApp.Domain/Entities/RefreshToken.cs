// ============================================================
// ChatApp.Domain/Entities/RefreshToken.cs
// NEW FILE
// ============================================================
namespace ChatApp.Domain.Entities;

public class RefreshTokenFamily
{
    public Guid FamilyId { get; init; }
    public Guid UserId { get; init; }
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = "Unknown Device";
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public string? RevokedReason { get; init; }
    public bool IsRevoked { get; init; }
}

public class RefreshToken
{
    public Guid TokenId { get; init; }
    public Guid FamilyId { get; init; }
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = default!;
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UsedAtUtc { get; init; }
    public bool IsUsed { get; init; }
    public bool IsRevoked { get; init; }
    public Guid? ReplacedByTokenId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    // Joined from family
    public bool FamilyIsRevoked { get; init; }
    public string? FamilyRevokedReason { get; init; }
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = "Unknown Device";
}

public class UserSession
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = "Unknown Device";
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastActiveUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool IsActive { get; init; }
}

public class AccountLockout
{
    public Guid LockoutId { get; init; }
    public Guid UserId { get; init; }
    public DateTime LockedAtUtc { get; init; }
    public DateTime UnlocksAtUtc { get; init; }
    public bool IsActive { get; init; }
}
