using ChatApp.Application.Interfaces.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace ChatApp.Application.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is missing");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<(string? Url, string? PublicId, string? Error)> UploadImageAsync(Stream fileStream, string fileName)
    {
        try
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(fileName, fileStream),
                Folder = "chat-app/images",
                Transformation = new Transformation()
                    .Width(1000)
                    .Height(1000)
                    .Crop("limit")
                    .Quality("auto"),
                AllowedFormats = new[] { "jpg", "jpeg", "png", "gif", "webp" }
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                return (null, null, uploadResult.Error.Message);
            }

            return (uploadResult.SecureUrl.ToString(), uploadResult.PublicId, null);
        }
        catch (Exception ex)
        {
            return (null, null, ex.Message);
        }
    }

    public async Task<(string? Url, string? PublicId, string? Error)> UploadVideoAsync(Stream fileStream, string fileName)
    {
        try
        {
            var uploadParams = new VideoUploadParams()
            {
                File = new FileDescription(fileName, fileStream),
                Folder = "chat-app/videos",
                Transformation = new Transformation()
                    .Width(1280)
                    .Height(720)
                    .Crop("limit")
                    .Quality("auto"),
                AllowedFormats = new[] { "mp4", "mov", "avi", "webm" }
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                return (null, null, uploadResult.Error.Message);
            }

            return (uploadResult.SecureUrl.ToString(), uploadResult.PublicId, null);
        }
        catch (Exception ex)
        {
            return (null, null, ex.Message);
        }
    }

    public async Task<bool> DeleteFileAsync(string publicId)
    {
        try
        {
            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image
            };

            var result = await _cloudinary.DestroyAsync(deletionParams);
            return result.Result == "ok";
        }
        catch
        {
            return false;
        }
    }
}