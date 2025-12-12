namespace ChatApp.Application.Interfaces.IServices;

public interface ICloudinaryService
{
    Task<(string? Url, string? PublicId, string? Error)> UploadImageAsync(Stream fileStream, string fileName);
    Task<(string? Url, string? PublicId, string? Error)> UploadVideoAsync(Stream fileStream, string fileName);
    Task<bool> DeleteFileAsync(string publicId);
}