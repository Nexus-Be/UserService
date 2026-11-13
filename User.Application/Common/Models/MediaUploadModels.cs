namespace User.Application.Common.Models;

using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

public sealed record UploadMediaFileInput(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record UploadMediaFilesResponse(
    [property: JsonPropertyName("fileUrls")] IReadOnlyList<string> FileUrls);
