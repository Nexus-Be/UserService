namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Configuration;
using global::User.Application.Services.Auth;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Services.Jwt;
using Xunit;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_IncludesPermissionsClaims_WhenPermissionsProvided()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "http://localhost:8001",
                ["Jwt:Audience"] = "shoppingcart-api",
                ["Jwt:Signature"] = "TestSignatureKeyWithAtLeast32CharactersLong!",
                ["Jwt:ExpirationInMinutes"] = "60"
            })
            .Build();

        var generator = new JwtTokenGenerator(config);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@nexus.local",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };
        var roles = new[] { "seller", "buyer" };
        var permissions = new[] { "PRODUCT_CREATE", "PRODUCT_UPDATE" };

        var tokenString = generator.GenerateToken(user, roles, permissions, out var expiresAt);
        Assert.NotNull(tokenString);
        Assert.True(expiresAt > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        var permissionClaims = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Contains("PRODUCT_CREATE", permissionClaims);
        Assert.Contains("PRODUCT_UPDATE", permissionClaims);

        var roleClaims = jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("seller", roleClaims);
        Assert.Contains("buyer", roleClaims);

        var userIdClaims = jwt.Claims.Where(c => c.Type == "userId").Select(c => c.Value).ToList();
        Assert.Contains(user.Id.ToString(), userIdClaims);

        var emailClaims = jwt.Claims.Where(c => c.Type == "email").Select(c => c.Value).ToList();
        Assert.Contains("test@nexus.local", emailClaims);
    }

    [Fact]
    public void LoginAndRefreshResponses_ContainEnrichedPayloadFields()
    {
        var userResp = new LoginUserApiResponse(
            Id: "11111111-1111-1111-1111-111111111111",
            Email: "user@nexus.local",
            Username: "nexususer",
            FirstName: "Nexus",
            LastName: "User",
            Roles: new[] { "seller" },
            Permissions: new[] { "PRODUCT.CREATE" },
            UserId: "11111111-1111-1111-1111-111111111111",
            Role: "seller"
        );

        var loginResp = new LoginApiResponse(
            AccessToken: "token",
            RefreshToken: "refresh",
            TokenType: "Bearer",
            ExpiresIn: 3600,
            User: userResp,
            UserId: "11111111-1111-1111-1111-111111111111",
            Email: "user@nexus.local",
            Role: "seller",
            Roles: new[] { "seller" },
            Permissions: new[] { "PRODUCT.CREATE" }
        );

        Assert.Equal("11111111-1111-1111-1111-111111111111", loginResp.UserId);
        Assert.Equal("user@nexus.local", loginResp.Email);
        Assert.Equal("seller", loginResp.Role);
        Assert.Contains("PRODUCT.CREATE", loginResp.Permissions!);

        var refreshResp = new RefreshTokenApiResponse(
            AccessToken: "new-token",
            RefreshToken: "new-refresh",
            ExpiresIn: 3600,
            UserId: "11111111-1111-1111-1111-111111111111",
            Email: "user@nexus.local",
            Role: "seller",
            Roles: new[] { "seller" },
            Permissions: new[] { "PRODUCT.CREATE" },
            User: userResp
        );

        Assert.Equal("11111111-1111-1111-1111-111111111111", refreshResp.UserId);
        Assert.Equal("user@nexus.local", refreshResp.Email);
        Assert.Equal("seller", refreshResp.Role);
        Assert.NotNull(refreshResp.User);
    }
}
