namespace User.Application.Interfaces;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed record StoredFileResult(
    string Url,
    string PublicId,
    long SizeBytes,
    string ContentType);

public interface IFileStorageService
{
    Task<StoredFileResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        bool isPrivate = false,
        bool isVideo = false,
        CancellationToken cancellationToken = default);

    string GenerateSignedUrl(string publicId, bool isVideo = false, int expiresInMinutes = 10);
}
