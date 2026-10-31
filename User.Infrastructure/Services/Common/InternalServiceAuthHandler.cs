namespace User.Infrastructure.Services.Common;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class InternalServiceAuthHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalServiceAuthHandler> _logger;
    private bool _missingKeyLogged;

    public const string HeaderKeyInternalService = "X-Internal-Service-Key";

    public InternalServiceAuthHandler(IConfiguration configuration, ILogger<InternalServiceAuthHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var serviceKey = Environment.GetEnvironmentVariable("INTERNAL_SERVICE_SECRET")
            ?? _configuration["INTERNAL_SERVICE_SECRET"]
            ?? _configuration["InternalSecurity:ServiceKey"];

        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            if (!_missingKeyLogged)
            {
                _missingKeyLogged = true;
                _logger.LogWarning("Internal service key is not configured. Outbound internal requests will be rejected by downstream services.");
            }
        }
        else if (!request.Headers.Contains(HeaderKeyInternalService))
        {
            request.Headers.Add(HeaderKeyInternalService, serviceKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
