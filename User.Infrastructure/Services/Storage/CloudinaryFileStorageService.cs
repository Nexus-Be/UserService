namespace User.Infrastructure.Services.Storage;

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Exceptions;

public sealed class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    public CloudinaryFileStorageService(
        string cloudName,
        string apiKey,
        string apiSecret,
        ILogger<CloudinaryFileStorageService> logger)
    {
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        _logger = logger;
    }

    public async Task<StoredFileResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        bool isPrivate = false,
        bool isVideo = false,
        CancellationToken cancellationToken = default)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var cleanFolder = $"nexus/users/{folder.Trim('/')}";
        var publicId = $"{Guid.NewGuid():N}";
        var deliveryType = isPrivate ? "authenticated" : "upload";
        var isPdf = string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);

        UploadResult result;
        if (isVideo)
        {
            var videoParams = new VideoUploadParams
            {
                File = new FileDescription(fileName, content),
                Folder = cleanFolder,
                PublicId = publicId,
                Type = deliveryType
            };
            result = await _cloudinary.UploadAsync(videoParams, cancellationToken);
        }
        else if (isPdf)
        {
            var rawParams = new RawUploadParams
            {
                File = new FileDescription(fileName, content),
                Folder = cleanFolder,
                PublicId = $"{publicId}.pdf",
                Type = deliveryType
            };
            result = await _cloudinary.UploadAsync(rawParams, "raw", cancellationToken);
        }
        else
        {
            var imageParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, content),
                Folder = cleanFolder,
                PublicId = publicId,
                Type = deliveryType
            };
            result = await _cloudinary.UploadAsync(imageParams, cancellationToken);
        }

        if (result.Error != null || result.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("[STORAGE] Cloudinary upload failed: {Error}", result.Error?.Message ?? result.StatusCode.ToString());
            throw new DomainException("STORAGE_UPLOAD_FAILED", "Internal server error occurred while storing uploaded file.", 500);
        }

        var url = isPrivate
            ? GenerateSignedUrl(result.PublicId, isVideo)
            : result.SecureUrl?.ToString() ?? result.Url?.ToString() ?? string.Empty;

        return new StoredFileResult(
            Url: url,
            PublicId: result.PublicId,
            SizeBytes: result.Bytes,
            ContentType: contentType);
    }

    public string GenerateSignedUrl(string publicId, bool isVideo = false, int expiresInMinutes = 10)
    {
        var resourceType = isVideo ? "video" : (publicId.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "raw" : "image");
        var ttlMinutes = expiresInMinutes > 0 ? expiresInMinutes : 10;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes).ToUnixTimeSeconds();

        return _cloudinary.DownloadPrivate(
            publicId: publicId,
            attachment: false,
            format: null,
            type: "authenticated",
            expiresAt: expiresAt,
            resourceType: resourceType);
    }
}
