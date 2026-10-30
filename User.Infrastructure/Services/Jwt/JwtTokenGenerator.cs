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
        var issuer = _configuration["Jwt:Issuer"] ?? "http://localhost:8001";
        var audience = _configuration["Jwt:Audience"] ?? "shoppingcart-api";
        var secretKey = _configuration["Jwt:Signature"];

        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Contains("REPLACE_WITH"))
        {
            secretKey = "NexusDefaultSecretKeyForJwtAuthentication2026@SafeKeyMin32Chars";
        }

        var expirationInMinutes = 120;
        if (int.TryParse(_configuration["Jwt:ExpirationInMinutes"], out var configuredMinutes) && configuredMinutes > 0)
        {
            expirationInMinutes = configuredMinutes;
        }

        expiresAt = DateTime.UtcNow.AddMinutes(expirationInMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.ToLowerInvariant()));
                    claims.Add(new Claim("role", role.ToLowerInvariant()));
                }
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
