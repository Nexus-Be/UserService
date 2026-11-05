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
[Route("api/v1/admin/sellers")]
[RequireRole("admin", "staff")]
public sealed class AdminSellersController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<AdminSellersController> _logger;

    public AdminSellersController(
        ISellerService sellerService,
        ILogger<AdminSellersController> logger)
    {
        _sellerService = sellerService ?? throw new ArgumentNullException(nameof(sellerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>[Admin / Staff] Retrieves detailed seller profile including KYC, warehouse, and reputation info.</summary>
    [HttpGet("{sellerId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminSellerProfileResponseDto))]
    public async Task<IActionResult> GetAdminSellerProfile(
        [FromRoute] Guid sellerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Admin GetAdminSellerProfile] Fetching seller profile for identifier: '{SellerId}'", sellerId);
        var result = await _sellerService.GetAdminSellerProfileAsync(sellerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Admin] Suspends a seller shop from selling.</summary>
    [HttpPut("{sellerId:guid}/suspend")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SuspendSellerResponseDto))]
    public async Task<IActionResult> SuspendSeller(
        [FromRoute] Guid sellerId,
        [FromBody] SuspendSellerDto dto,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin SuspendSeller] Suspending seller '{SellerId}' by admin '{AdminId}'", sellerId, adminId);
        var result = await _sellerService.SuspendSellerAsync(sellerId, dto, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Admin] Reactivates a suspended seller shop.</summary>
    [HttpPut("{sellerId:guid}/reactivate")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReactivateSellerResponseDto))]
    public async Task<IActionResult> ReactivateSeller(
        [FromRoute] Guid sellerId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ReactivateSellerDto? dto,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin ReactivateSeller] Reactivating seller '{SellerId}' by admin '{AdminId}'", sellerId, adminId);
        var result = await _sellerService.ReactivateSellerAsync(sellerId, dto, adminId, cancellationToken);
        return Ok(result);
    }
}
