namespace User.Infrastructure.Services.Storage;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _contentRootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IHostEnvironment environment, ILogger<LocalFileStorageService> logger)
    {
        _contentRootPath = environment.ContentRootPath;
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

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var cleanFolder = folder.Trim('/');
        var dateSegment = DateTime.UtcNow.ToString("yyyy/MM");
        var storedFileName = $"{Guid.NewGuid():N}{ext}";

        var baseDir = isPrivate
            ? Path.Combine(_contentRootPath, "App_Data", "uploads", cleanFolder, dateSegment)
            : Path.Combine(_contentRootPath, "wwwroot", "uploads", cleanFolder, dateSegment);

        Directory.CreateDirectory(baseDir);

        var fullPath = Path.Combine(baseDir, storedFileName);
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        var relativeKey = $"{cleanFolder}/{dateSegment}/{storedFileName}";
        var url = isPrivate
            ? $"/api/v1/sellers/registration/documents/file?key={Uri.EscapeDataString(relativeKey)}"
            : $"/uploads/{relativeKey}";

        _logger.LogInformation("[STORAGE] Saved local file to '{Path}' (Private: {IsPrivate})", fullPath, isPrivate);

        return new StoredFileResult(
            Url: url,
            PublicId: relativeKey,
            SizeBytes: content.CanSeek ? content.Length : new FileInfo(fullPath).Length,
            ContentType: contentType);
    }

    public string GenerateSignedUrl(string publicId, bool isVideo = false, int expiresInMinutes = 10)
    {
        return $"/api/v1/sellers/registration/documents/file?key={Uri.EscapeDataString(publicId)}";
    }
}
