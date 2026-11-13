namespace User.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Interfaces;
using User.Application.Services.Profile;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AvatarUploadTests
{
    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"avatar_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Domain.Entities.User> SeedUserAsync(AppDbContext dbContext, string email)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.Buyer,
            RoleName = "Buyer",
            IsActive = true,
            IsSystemRole = true
        };
        dbContext.Roles.Add(role);

        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            FirstName = "Avatar",
            LastName = "Tester",
            Status = UserStatus.Active,
            RoleId = role.Id
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task UploadMyAvatarAsync_uploads_valid_png_and_updates_user_avatarUrl()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserAsync(dbContext, "avatar.ok@nexus.local");
        var storage = new StubFileStorageService();
        var service = new ProfileService(dbContext, NullLogger<ProfileService>.Instance, storage);

        await using var stream = new MemoryStream(PngHeader);
        var result = await service.UploadMyAvatarAsync(user.Id, stream, "avatar.png", "image/png", stream.Length);

        Assert.Equal("https://res.cloudinary.com/demo/image/upload/avatars/avatar.png", result.AvatarUrl);
        Assert.Equal("avatars", storage.LastFolder);
        Assert.False(storage.LastIsPrivate);

        var updatedUser = await dbContext.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(result.AvatarUrl, updatedUser.AvatarUrl);
    }

    [Fact]
    public async Task UploadMyAvatarAsync_rejects_file_over_2MB_and_invalid_magic_bytes()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserAsync(dbContext, "avatar.fail@nexus.local");
        var service = new ProfileService(dbContext, NullLogger<ProfileService>.Instance, new StubFileStorageService());

        // Over 2MB
        await using var tooLargeStream = new MemoryStream(PngHeader);
        var tooLargeEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.UploadMyAvatarAsync(user.Id, tooLargeStream, "big.png", "image/png", (2 * 1024 * 1024) + 1));
        Assert.Equal(413, tooLargeEx.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", tooLargeEx.ErrorCode);

        // Spoofed .png with text bytes
        await using var fakePng = new MemoryStream("not-a-png-file"u8.ToArray());
        var invalidEx = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UploadMyAvatarAsync(user.Id, fakePng, "fake.png", "image/png", fakePng.Length));
        Assert.Equal("INVALID_FILE_TYPE", invalidEx.ErrorCode);
    }

    [Fact]
    public async Task Controllers_UploadMyAvatar_GetMyAvatar_and_GetUserAvatar_AllowAnonymous_work()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserAsync(dbContext, "avatar.ctrl@nexus.local");
        var service = new ProfileService(dbContext, NullLogger<ProfileService>.Instance, new StubFileStorageService());

        var meController = new MeController(service, NullLogger<MeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.Id.ToString()) }, "TestAuth"))
                }
            }
        };

        await using var ms = new MemoryStream(PngHeader);
        var formFile = new FormFile(ms, 0, ms.Length, "file", "me.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var uploadRes = await meController.UploadMyAvatar(formFile);
        var okUpload = Assert.IsType<OkObjectResult>(uploadRes);
        var uploadDto = Assert.IsType<UploadAvatarResponse>(okUpload.Value);
        Assert.Contains("cloudinary", uploadDto.AvatarUrl);

        var myAvatarRes = await meController.GetMyAvatar();
        var okMy = Assert.IsType<OkObjectResult>(myAvatarRes);
        var myDto = Assert.IsType<UserAvatarResponse>(okMy.Value);
        Assert.Equal(uploadDto.AvatarUrl, myDto.AvatarUrl);

        var usersController = new UsersController(service, NullLogger<UsersController>.Instance);
        var publicRes = await usersController.GetUserAvatar(user.Id);
        var okPublic = Assert.IsType<OkObjectResult>(publicRes);
        var publicDto = Assert.IsType<UserAvatarResponse>(okPublic.Value);
        Assert.Equal(uploadDto.AvatarUrl, publicDto.AvatarUrl);

        // Verify [AllowAnonymous] on GetUserAvatar
        var methodInfo = typeof(UsersController).GetMethod(nameof(UsersController.GetUserAvatar));
        Assert.NotNull(methodInfo?.GetCustomAttributes<AllowAnonymousAttribute>().FirstOrDefault());

        // Missing user -> 404
        var notFoundEx = await Assert.ThrowsAsync<DomainException>(() => usersController.GetUserAvatar(Guid.NewGuid()));
        Assert.Equal(404, notFoundEx.StatusCode);
        Assert.Equal("USER_NOT_FOUND", notFoundEx.ErrorCode);
    }

    private sealed class StubFileStorageService : IFileStorageService
    {
        public string? LastFolder { get; private set; }
        public bool LastIsPrivate { get; private set; }

        public Task<StoredFileResult> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            string folder,
            bool isPrivate = false,
            bool isVideo = false,
            CancellationToken cancellationToken = default)
        {
            LastFolder = folder;
            LastIsPrivate = isPrivate;
            var url = isPrivate
                ? $"https://res.cloudinary.com/demo/image/authenticated/s--sig--/{folder}/{fileName}"
                : $"https://res.cloudinary.com/demo/image/upload/{folder}/{fileName}";

            return Task.FromResult(new StoredFileResult(url, $"{folder}/{fileName}", content.Length, contentType));
        }

        public string GenerateSignedUrl(string publicId, bool isVideo = false, int expiresInMinutes = 10)
            => $"https://res.cloudinary.com/demo/image/authenticated/s--sig--/{publicId}";
    }
}
