namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Api.Filters;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Application.Services.Profile;
using User.Application.Services.Report;
using User.Application.Services.Review;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class MediaUploadTests
{
    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
    private static readonly byte[] Mp4Header = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 };

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"media_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static void SetUserContext(ControllerBase controller, Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        }, "TestAuth");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public async Task UploadReportFilesAsync_uploads_images_and_video_within_limits()
    {
        using var dbContext = CreateInMemoryDbContext();
        var storage = new StubMediaStorageService();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance, storage);

        await using var imgStream = new MemoryStream(PngHeader);
        await using var vidStream = new MemoryStream(Mp4Header);
        var files = new List<UploadMediaFileInput>
        {
            new(imgStream, "evidence.png", "image/png", imgStream.Length),
            new(vidStream, "clip.mp4", "video/mp4", vidStream.Length)
        };

        var result = await service.UploadReportFilesAsync(Guid.NewGuid(), files);

        Assert.Equal(2, result.FileUrls.Count);
        Assert.All(storage.Folders, f => Assert.StartsWith("reports/", f));
        Assert.Contains("evidence.png", result.FileUrls[0]);
        Assert.Contains("clip.mp4", result.FileUrls[1]);
    }

    [Fact]
    public async Task UploadReportFilesAsync_throws_400_when_more_than_5_files()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance, new StubMediaStorageService());

        var files = Enumerable.Range(1, 6)
            .Select(i => new UploadMediaFileInput(new MemoryStream(PngHeader), $"img{i}.png", "image/png", PngHeader.Length))
            .ToList();

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReportFilesAsync(Guid.NewGuid(), files));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("FILE_LIMIT_EXCEEDED", ex.ErrorCode);
    }

    [Fact]
    public async Task UploadReportFilesAsync_throws_400_when_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance, new StubMediaStorageService());

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReportFilesAsync(Guid.NewGuid(), Array.Empty<UploadMediaFileInput>()));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task UploadReportFilesAsync_throws_413_when_video_exceeds_20mb()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance, new StubMediaStorageService());

        await using var vidStream = new MemoryStream(Mp4Header);
        var files = new List<UploadMediaFileInput>
        {
            new(vidStream, "huge.mp4", "video/mp4", 21L * 1024 * 1024)
        };

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReportFilesAsync(Guid.NewGuid(), files));
        Assert.Equal(413, ex.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", ex.ErrorCode);
    }

    [Fact]
    public async Task UploadReportFilesAsync_throws_400_when_magic_bytes_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance, new StubMediaStorageService());

        await using var badStream = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 });
        var files = new List<UploadMediaFileInput>
        {
            new(badStream, "malware.png", "image/png", badStream.Length)
        };

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReportFilesAsync(Guid.NewGuid(), files));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("INVALID_FILE_TYPE", ex.ErrorCode);
    }

    [Fact]
    public void UsersController_UploadReportFile_requires_admin_staff_seller_buyer_roles()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.UploadReportFile));
        Assert.NotNull(method);

        var attrData = method!.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(RequireRoleAttribute));
        Assert.NotNull(attrData);

        var roles = ((IEnumerable<CustomAttributeTypedArgument>)attrData!.ConstructorArguments[0].Value!)
            .Select(x => (string)x.Value!)
            .ToArray();
        Assert.Equal(new[] { "admin", "staff", "seller", "buyer" }, roles);
    }

    [Fact]
    public async Task UsersController_UploadReportFile_returns_200_with_fileUrls()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);
        var reportService = new ReportService(dbContext, NullLogger<ReportService>.Instance, new StubMediaStorageService());
        var controller = new UsersController(
            profileService,
            NullLogger<UsersController>.Instance,
            reportService: reportService);

        SetUserContext(controller, Guid.NewGuid());
        var formFiles = new List<IFormFile>
        {
            CreateFormFile(PngHeader, "report.png", "image/png")
        };

        var result = await controller.UploadReportFile(formFiles, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<UploadMediaFilesResponse>(okResult.Value);
        Assert.Single(payload.FileUrls);
    }

    [Fact]
    public async Task UploadReviewFilesAsync_uploads_images_within_limits()
    {
        using var dbContext = CreateInMemoryDbContext();
        var storage = new StubMediaStorageService();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance, storage);

        await using var imgStream = new MemoryStream(PngHeader);
        var files = new List<UploadMediaFileInput>
        {
            new(imgStream, "review.png", "image/png", imgStream.Length)
        };

        var result = await service.UploadReviewFilesAsync(Guid.NewGuid(), files);

        Assert.Single(result.FileUrls);
        Assert.All(storage.Folders, f => Assert.StartsWith("reviews/", f));
        Assert.Contains("review.png", result.FileUrls[0]);
    }

    [Fact]
    public async Task UploadReviewFilesAsync_throws_400_when_more_than_5_files()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance, new StubMediaStorageService());

        var files = Enumerable.Range(1, 6)
            .Select(i => new UploadMediaFileInput(new MemoryStream(PngHeader), $"rev{i}.png", "image/png", PngHeader.Length))
            .ToList();

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReviewFilesAsync(Guid.NewGuid(), files));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("FILE_LIMIT_EXCEEDED", ex.ErrorCode);
    }

    [Fact]
    public async Task UploadReviewFilesAsync_rejects_video_files_with_400()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance, new StubMediaStorageService());

        await using var vidStream = new MemoryStream(Mp4Header);
        var files = new List<UploadMediaFileInput>
        {
            new(vidStream, "review.mp4", "video/mp4", vidStream.Length)
        };

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReviewFilesAsync(Guid.NewGuid(), files));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("INVALID_FILE_TYPE", ex.ErrorCode);
    }

    [Fact]
    public async Task UploadReviewFilesAsync_throws_413_when_image_exceeds_5mb()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance, new StubMediaStorageService());

        await using var imgStream = new MemoryStream(PngHeader);
        var files = new List<UploadMediaFileInput>
        {
            new(imgStream, "big.png", "image/png", 6L * 1024 * 1024)
        };

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => service.UploadReviewFilesAsync(Guid.NewGuid(), files));
        Assert.Equal(413, ex.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", ex.ErrorCode);
    }

    [Fact]
    public void ReviewsController_UploadReviewFile_requires_buyer_and_seller_roles()
    {
        var method = typeof(ReviewsController).GetMethod(nameof(ReviewsController.UploadReviewFile));
        Assert.NotNull(method);

        var attrData = method!.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(RequireRoleAttribute));
        Assert.NotNull(attrData);

        var roles = ((IEnumerable<CustomAttributeTypedArgument>)attrData!.ConstructorArguments[0].Value!)
            .Select(x => (string)x.Value!)
            .ToArray();
        Assert.Equal(new[] { "buyer", "seller" }, roles);
    }

    [Fact]
    public async Task ReviewsController_UploadReviewFile_returns_200_with_fileUrls()
    {
        using var dbContext = CreateInMemoryDbContext();
        var reviewService = new ReviewService(dbContext, NullLogger<ReviewService>.Instance, new StubMediaStorageService());
        var controller = new ReviewsController(reviewService, NullLogger<ReviewsController>.Instance);

        SetUserContext(controller, Guid.NewGuid());
        var formFiles = new List<IFormFile>
        {
            CreateFormFile(PngHeader, "review.png", "image/png")
        };

        var result = await controller.UploadReviewFile(formFiles, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<UploadMediaFilesResponse>(okResult.Value);
        Assert.Single(payload.FileUrls);
    }

    private sealed class StubMediaStorageService : IFileStorageService
    {
        public List<string> Folders { get; } = new();

        public Task<StoredFileResult> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            string folder,
            bool isPrivate = false,
            bool isVideo = false,
            CancellationToken cancellationToken = default)
        {
            Folders.Add(folder);
            var publicId = $"{folder}/{fileName}";
            var url = $"https://res.cloudinary.com/demo/image/upload/{publicId}";
            return Task.FromResult(new StoredFileResult(url, publicId, content.Length, contentType));
        }

        public string GenerateSignedUrl(string publicId, bool isVideo = false, int expiresInMinutes = 10)
            => $"https://res.cloudinary.com/demo/image/authenticated/{publicId}?exp={expiresInMinutes}";
    }
}
