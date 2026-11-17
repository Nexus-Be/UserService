namespace User.Application.Interfaces;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed record ImageModerationResult(
    bool IsAllowed,
    string ViolationCategory = "None",
    double Confidence = 0,
    string? Reason = null);

public interface IImageModerationService
{
    Task<ImageModerationResult> ModerateAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
