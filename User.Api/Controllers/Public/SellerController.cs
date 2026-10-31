namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Seller;

[ApiController]
[Route("api/v1/sellers")]
public sealed class SellerController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<SellerController> _logger;

    public SellerController(ISellerService sellerService, ILogger<SellerController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>Submits a seller onboarding registration application.</summary>
    [HttpPost("registration")]
    [RequireRole]
    public async Task<IActionResult> RegisterSeller(
        [FromBody] CreateSellerRegistrationDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST RegisterSeller] Submitting seller onboarding for UserId: '{UserId}', Shop: '{ShopName}'", userId, request?.ShopName);
        var result = await _sellerService.RegisterSellerAsync(userId, request!, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Retrieves own seller profile.</summary>
    [HttpGet("profile")]
    [RequireRole("seller", "admin")]
    public async Task<IActionResult> GetSellerOwnProfile(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetSellerOwnProfile] Fetching seller profile for UserId: '{UserId}'", userId);
        var result = await _sellerService.GetSellerOwnProfileAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates own seller profile and warehouse address.</summary>
    [HttpPatch("profile")]
    [RequireRole("seller", "admin")]
    public async Task<IActionResult> UpdateSellerOwnProfile(
        [FromBody] UpdateSellerOwnProfileDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST UpdateSellerOwnProfile] Updating seller profile for UserId: '{UserId}'", userId);
        var result = await _sellerService.UpdateSellerOwnProfileAsync(userId, request, cancellationToken);
        return Ok(result);
    }
}
