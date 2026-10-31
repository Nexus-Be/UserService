namespace User.Infrastructure.Services.Jwt;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using User.Application.Interfaces;
using User.Domain.Entities;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, IEnumerable<string> roles, IEnumerable<string>? permissions, out DateTime expiresAt)
    {
        var issuer = _configuration["Jwt:Issuer"]
            ?? _configuration["JWT_ISSUER"]
            ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? "http://localhost:8001";

        var audience = _configuration["Jwt:Audience"]
            ?? _configuration["JWT_AUDIENCE"]
            ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
            ?? "shoppingcart-api";

        var secretKey = _configuration["Jwt:Signature"]
            ?? _configuration["Jwt:SecretKey"]
            ?? _configuration["JWT_SIGNATURE"]
            ?? _configuration["JWT_SECRET"]
            ?? Environment.GetEnvironmentVariable("JWT_SIGNATURE")
            ?? Environment.GetEnvironmentVariable("JWT_SECRET");

        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Contains("REPLACE_WITH") || Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key is missing, contains placeholder or is shorter than 32 bytes. Set 'Jwt:Signature' or environment variable 'JWT_SIGNATURE'.");
        }

        var expirationInMinutes = 60;
        var expirationConfig = _configuration["Jwt:ExpirationInMinutes"]
            ?? _configuration["JWT_EXPIRATION_IN_MINUTES"]
            ?? Environment.GetEnvironmentVariable("JWT_EXPIRATION_IN_MINUTES");

        if (int.TryParse(expirationConfig, out var configuredMinutes) && configuredMinutes > 0)
        {
            expirationInMinutes = configuredMinutes;
        }

        expiresAt = DateTime.UtcNow.AddMinutes(expirationInMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("userId", user.Id.ToString()),
            new("id", user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("email", user.Email),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null)
        {
            var roleList = roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            foreach (var role in roleList)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
                claims.Add(new Claim("roles", role));
            }
        }

        if (permissions != null)
        {
            var cleanPermissions = permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var cleanPermission in cleanPermissions)
            {
                claims.Add(new Claim("permission", cleanPermission));
                claims.Add(new Claim("permissions", cleanPermission));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    public string GenerateToken(User user, IEnumerable<string> roles, out DateTime expiresAt) =>
        GenerateToken(user, roles, null, out expiresAt);
}
