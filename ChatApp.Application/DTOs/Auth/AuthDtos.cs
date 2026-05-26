using ChatApp.Application.DTOs.User;

namespace ChatApp.Application.DTOs.Auth;

public record LoginRequest(string UserName, string Password);
public record RegisterRequest(string UserName, string DisplayName, string Password);
public record AuthResponse(string Token, UserDto User);
// DTOs
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
