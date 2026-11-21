namespace User.Infrastructure.Services.Auth;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using User.Application.Interfaces;

public sealed class FirebaseAuthService : IFirebaseAuthService
{
    public const string DefaultJwksUrl = "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";
    private const string JwksCacheKey = "firebase:jwks:keys";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FirebaseAuthService> _logger;

    public FirebaseAuthService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IConfiguration configuration,
        ILogger<FirebaseAuthService> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<FirebaseUserClaims> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new SecurityTokenException("Firebase ID token is empty.");
        }

        var projectId = _configuration["Firebase:ProjectId"]
            ?? _configuration["FIREBASE_PROJECT_ID"]
            ?? Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");

        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Firebase ProjectId is not configured. Set 'Firebase:ProjectId' or environment variable 'FIREBASE_PROJECT_ID'.");
        }

        projectId = projectId.Trim();
        var keys = await GetSigningKeysAsync(forceRefresh: false, cancellationToken);

        ClaimsPrincipal principal;
        try
        {
            principal = ValidateToken(idToken, projectId, keys);
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogInformation("[FIREBASE AUTH] Token signature validation failed; refreshing Google JWKS cache and retrying.");
            keys = await GetSigningKeysAsync(forceRefresh: true, cancellationToken);
            principal = ValidateToken(idToken, projectId, keys);
        }

        var uid = principal.FindFirst("user_id")?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new SecurityTokenException("Firebase ID token does not contain a valid subject (uid).");
        }

        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? string.Empty;

        var emailVerifiedRaw = principal.FindFirst("email_verified")?.Value;
        var emailVerified = bool.TryParse(emailVerifiedRaw, out var parsedVerified) && parsedVerified;

        var displayName = principal.FindFirst("name")?.Value;
        var givenName = principal.FindFirst("given_name")?.Value;
        var familyName = principal.FindFirst("family_name")?.Value;
        var pictureUrl = principal.FindFirst("picture")?.Value;

        return new FirebaseUserClaims(
            Uid: uid.Trim(),
            Email: email.Trim(),
            EmailVerified: emailVerified,
            DisplayName: displayName,
            GivenName: givenName,
            FamilyName: familyName,
            PictureUrl: pictureUrl);
    }

    private static ClaimsPrincipal ValidateToken(
        string idToken,
        string projectId,
        IReadOnlyCollection<SecurityKey> signingKeys)
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{projectId}",
            ValidateAudience = true,
            ValidAudience = projectId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            RequireSignedTokens = true,
            RequireExpirationTime = true
        };

        var principal = handler.ValidateToken(idToken, validationParameters, out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwtToken ||
            !string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenInvalidAlgorithmException("Firebase ID token must be signed with RS256.");
        }

        return principal;
    }

    private async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh && _memoryCache.TryGetValue(JwksCacheKey, out IReadOnlyCollection<SecurityKey>? cachedKeys) && cachedKeys is { Count: > 0 })
        {
            return cachedKeys;
        }

        var jwksUrl = _configuration["Firebase:JwksUrl"];
        if (string.IsNullOrWhiteSpace(jwksUrl))
        {
            jwksUrl = DefaultJwksUrl;
        }

        using var response = await _httpClient.GetAsync(jwksUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jwksJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var keySet = new JsonWebKeySet(jwksJson);
        var keys = keySet.GetSigningKeys().ToArray();

        if (keys.Length == 0)
        {
            throw new SecurityTokenException("Google JWKS endpoint returned no signing keys.");
        }

        var cacheDuration = TimeSpan.FromHours(1);
        if (response.Headers.CacheControl?.MaxAge is TimeSpan maxAge && maxAge > TimeSpan.Zero)
        {
            cacheDuration = maxAge < TimeSpan.FromMinutes(5)
                ? TimeSpan.FromMinutes(5)
                : maxAge > TimeSpan.FromHours(24)
                    ? TimeSpan.FromHours(24)
                    : maxAge;
        }

        _memoryCache.Set(JwksCacheKey, (IReadOnlyCollection<SecurityKey>)keys, cacheDuration);
        return keys;
    }
}
