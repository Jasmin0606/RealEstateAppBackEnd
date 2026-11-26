using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Media;

public class CloudinaryMediaService : IMediaService
{
    private readonly Cloudinary _cloudinary;
    private const string PhotoFolder = "realestate/photos";
    private const string VideoFolder = "realestate/videos";

    public CloudinaryMediaService(IConfiguration config)
    {
        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public async Task<(string Url, string PublicId)> UploadPhotoAsync(Stream stream, string fileName)
    {
        var uploadParams = new ImageUploadParams
        {
            File           = new FileDescription(fileName, stream),
            Folder         = PhotoFolder,
            Transformation = new Transformation()
                .Width(1200).Height(800).Crop("limit")   // Max 1200x800
                .Quality("auto").FetchFormat("auto"),     // Auto WebP/AVIF
            UseFilename    = false,
            UniqueFilename = true
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return (result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task<(string Url, string PublicId)> UploadVideoAsync(Stream stream, string fileName)
    {
        var uploadParams = new VideoUploadParams
        {
            File           = new FileDescription(fileName, stream),
            Folder         = VideoFolder,
            UseFilename    = false,
            UniqueFilename = true
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary video upload failed: {result.Error.Message}");

        return (result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task<bool> DeleteAsync(string publicId)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        return result.Result == "ok";
    }
}
