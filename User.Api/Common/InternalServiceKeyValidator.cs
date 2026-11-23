using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace User.Api.Common;

internal static class InternalServiceKeyValidator
{
    public static bool TryValidate(HttpRequest request, IConfiguration configuration, out bool isConfigured)
    {
        var expected = Environment.GetEnvironmentVariable("INTERNAL_SERVICE_SECRET")
            ?? configuration["INTERNAL_SERVICE_SECRET"]
            ?? configuration["InternalSecurity:ServiceKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            isConfigured = false;
            return false;
        }

        isConfigured = true;
        var provided = request.Headers["X-Internal-Service-Key"].ToString();
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length > 0 && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
