namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
}
