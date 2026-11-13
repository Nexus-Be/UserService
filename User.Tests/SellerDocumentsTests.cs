namespace User.Tests;

using System;
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
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class SellerDocumentsTests
{
    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly byte[] PdfHeader = "%PDF-1.7\n"u8.ToArray();

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"seller_docs_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static SellerService CreateSellerService(AppDbContext dbContext, IFileStorageService storage)
        => new(
            dbContext,
            new StubOtpService(),
            null,
            new StubFulfillmentClient(),
            NullLogger<SellerService>.Instance,
            storage);

    [Fact]
    public async Task UploadSellerDocumentsAsync_uploads_private_kyc_assets_and_returns_signed_urls()
    {
        using var dbContext = CreateInMemoryDbContext();
        var storage = new CapturingPrivateStorageService();
        var service = CreateSellerService(dbContext, storage);
        var userId = Guid.NewGuid();

        await using var front = new MemoryStream(JpegHeader);
        await using var back = new MemoryStream(JpegHeader);
        await using var license = new MemoryStream(PdfHeader);

        var result = await service.UploadSellerDocumentsAsync(
            userId,
            new SellerDocumentFileInput(front, "front.jpg", "image/jpeg", front.Length),
            new SellerDocumentFileInput(back, "back.jpg", "image/jpeg", back.Length),
            new SellerDocumentFileInput(license, "license.pdf", "application/pdf", license.Length));

        Assert.True(storage.AllPrivate);
        Assert.Equal(3, result.DocumentUrls.Count);
        Assert.Contains("authenticated", result.IdCardFrontUrl);
        Assert.Contains("authenticated", result.IdCardBackUrl);
        Assert.NotNull(result.BusinessLicenseUrl);
    }

    [Fact]
    public async Task UploadSellerDocumentsAsync_validates_registrationId_ownership_and_file_constraints()
    {
        using var dbContext = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var profile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            ShopName = "Test Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "001",
            Hotline = "0901234567",
            Status = SellerProfileStatus.Pending
        };
        dbContext.SellerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext, new CapturingPrivateStorageService());

        await using var f1 = new MemoryStream(JpegHeader);
        await using var b1 = new MemoryStream(JpegHeader);

        // Missing registration -> 404
        var notFound = await Assert.ThrowsAsync<DomainException>(() => service.UploadSellerDocumentsAsync(
            ownerId,
            new SellerDocumentFileInput(f1, "f.jpg", "image/jpeg", f1.Length),
            new SellerDocumentFileInput(b1, "b.jpg", "image/jpeg", b1.Length),
            registrationId: Guid.NewGuid()));
        Assert.Equal(404, notFound.StatusCode);
        Assert.Equal("REGISTRATION_NOT_FOUND", notFound.ErrorCode);

        // Other user's registration -> 403
        var forbidden = await Assert.ThrowsAsync<DomainException>(() => service.UploadSellerDocumentsAsync(
            otherId,
            new SellerDocumentFileInput(f1, "f.jpg", "image/jpeg", f1.Length),
            new SellerDocumentFileInput(b1, "b.jpg", "image/jpeg", b1.Length),
            registrationId: profile.Id));
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", forbidden.ErrorCode);

        // File > 5MB -> 413
        var tooLarge = await Assert.ThrowsAsync<DomainException>(() => service.UploadSellerDocumentsAsync(
            ownerId,
            new SellerDocumentFileInput(f1, "f.jpg", "image/jpeg", (5 * 1024 * 1024) + 1),
            new SellerDocumentFileInput(b1, "b.jpg", "image/jpeg", b1.Length)));
        Assert.Equal(413, tooLarge.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", tooLarge.ErrorCode);

        // Valid update with own registrationId
        var ok = await service.UploadSellerDocumentsAsync(
            ownerId,
            new SellerDocumentFileInput(f1, "f.jpg", "image/jpeg", f1.Length),
            new SellerDocumentFileInput(b1, "b.jpg", "image/jpeg", b1.Length),
            registrationId: profile.Id);

        var updated = await dbContext.SellerProfiles.SingleAsync(p => p.Id == profile.Id);
        Assert.Equal(ok.IdCardFrontUrl, updated.IdCardFrontUrl);
        Assert.Equal(ok.IdCardBackUrl, updated.IdCardBackUrl);
    }

    [Fact]
    public async Task SellerController_UploadSellerDocuments_allows_authenticated_buyer()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext, new CapturingPrivateStorageService());
        var controller = new SellerController(service, NullLogger<SellerController>.Instance);

        var buyerId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", buyerId.ToString()),
                    new Claim(ClaimTypes.Role, "buyer")
                }, "TestAuth"))
            }
        };

        var methodInfo = typeof(SellerController).GetMethod(nameof(SellerController.UploadSellerDocuments));
        var requireRole = methodInfo?.GetCustomAttributes<RequireRoleAttribute>().FirstOrDefault();
        Assert.NotNull(requireRole);

        await using var ms1 = new MemoryStream(JpegHeader);
        await using var ms2 = new MemoryStream(JpegHeader);
        var front = new FormFile(ms1, 0, ms1.Length, "frontCccd", "front.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        var back = new FormFile(ms2, 0, ms2.Length, "backCccd", "back.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };

        var res = await controller.UploadSellerDocuments(front, back);
        var ok = Assert.IsType<OkObjectResult>(res);
        var dto = Assert.IsType<UploadSellerDocumentsResponseDto>(ok.Value);
        Assert.Equal(2, dto.DocumentUrls.Count);
    }

    private sealed class CapturingPrivateStorageService : IFileStorageService
    {
        public bool AllPrivate { get; private set; } = true;

        public Task<StoredFileResult> UploadAsync(Stream content, string fileName, string contentType, string folder, bool isPrivate = false, bool isVideo = false, CancellationToken cancellationToken = default)
        {
            if (!isPrivate) AllPrivate = false;
            var url = $"https://res.cloudinary.com/demo/image/authenticated/s--sig--/{folder}/{fileName}";
            return Task.FromResult(new StoredFileResult(url, $"{folder}/{fileName}", content.Length, contentType));
        }

        public string GenerateSignedUrl(string publicId, bool isVideo = false, int expiresInMinutes = 10)
            => $"https://res.cloudinary.com/demo/image/authenticated/s--sig--/{publicId}";
    }

    private sealed class StubOtpService : IRedisOtpService
    {
        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration") => Task.FromResult<OtpEntry?>(null);
        public Task MarkOtpUsedAsync(string email, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task DeleteOtpAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<bool> IsInCooldownAsync(string email, string purpose = "registration") => Task.FromResult(false);
        public Task SetCooldownAsync(string email, TimeSpan cooldownDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan windowDuration, string purpose = "registration") => Task.FromResult(0);
        public Task<int> GetOtpAttemptsAsync(string email, string purpose = "registration") => Task.FromResult(0);
        public Task ResetOtpAttemptsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan lockDuration) => Task.FromResult(0);
        public Task ResetLoginAttemptsAsync(string identifier) => Task.CompletedTask;
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan lockDuration) => Task.CompletedTask;
        public Task SavePasswordResetOtpAsync(string email, string code, TimeSpan validityDuration) => Task.CompletedTask;
        public Task<OtpEntry?> GetPasswordResetOtpAsync(string email) => Task.FromResult<OtpEntry?>(null);
        public Task MarkPasswordResetOtpUsedAsync(string email, TimeSpan retentionDuration) => Task.CompletedTask;
        public Task DeletePasswordResetOtpAsync(string email) => Task.CompletedTask;
        public Task<bool> IsPasswordResetInCooldownAsync(string email) => Task.FromResult(false);
        public Task SetPasswordResetCooldownAsync(string email, TimeSpan cooldownDuration) => Task.CompletedTask;
        public Task<int> IncrementPasswordResetAttemptsAsync(string email, TimeSpan windowDuration) => Task.FromResult(0);
        public Task<int> GetPasswordResetAttemptsAsync(string email) => Task.FromResult(0);
        public Task ResetPasswordResetAttemptsAsync(string email) => Task.CompletedTask;
    }

    private sealed class StubFulfillmentClient : IFulfillmentServiceClient
    {
        public Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string contactName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> UpdateWarehouseAsync(Guid sellerId, string shopName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
