namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Configuration;
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
    }
}
