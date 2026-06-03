// ============================================================
// ChatApp.Application/DTOs/Auth/AuthDtos.cs
// MODIFIED FILE — adds refresh token DTOs
// ============================================================
using ChatApp.Application.DTOs.User;
using ChatApp.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ChatApp.Application.DTOs.Auth;

// ── Inbound ─────────────────────────────────────────────────

public record LoginRequest(
    [Required] string UserName,
    [Required] string Password,
    string? DeviceName = null
);

public record RegisterRequest(
    [Required] string UserName,
    [Required] string DisplayName,
    [Required] string Password,
    [Required] string emailId
);

public record RefreshTokenRequest(
    [Required] string RefreshToken,
    [Required] Guid DeviceId
);

public record LogoutRequest(
    Guid? DeviceId = null,   // null = single device, omit to log out current
    bool GlobalLogout = false
);

// ── Outbound ────────────────────────────────────────────────

public class TokenResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public Guid DeviceId { get; set; }
    public int ExpiresIn { get; set; }  // seconds
    public UserDto User { get; set; } = default!;
}

public class SessionDto
{
    public Guid SessionId { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = "Unknown Device";
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastActiveUtc { get; set; }
    public bool IsCurrent { get; set; }
}

// ── Key backup (unchanged from original) ────────────────────
public class SaveKeyBackupRequest
{
    public string EncryptedKeyBackup { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
}

public class KeyBackupDto
{
    public string EncryptedKeyBackup { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
}

public class VerifyPinRequest { public string Pin { get; set; } = string.Empty; }

public class DeviceSwitchPinDto
{
    public string HashedPin { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
