namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Seller;

[ApiController]
[Route("api/v1/seller")]
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

    /// <summary>
    /// SELLER-01: Gửi đơn đăng ký mở Gian hàng Shop (Seller Onboarding).
    /// </summary>
    [HttpPost("registration")]
    [HttpPost("/api/v1/seller/registration")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSeller(
        [FromBody] CreateSellerRegistrationDto request,
        CancellationToken cancellationToken)
    {
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            _logger.LogWarning("[REST RegisterSeller] Missing or invalid x-user-id header.");
            return BadRequest(new
            {
                title = "Invalid user identifier",
                detail = "Missing or invalid x-user-id header.",
                errorCode = "VAL_INVALID_USER_ID"
            });
        }

        _logger.LogInformation("[REST RegisterSeller] Submitting seller onboarding for UserId: '{UserId}', Shop: '{ShopName}'", userId, request?.ShopName);

        var result = await _sellerService.RegisterSellerAsync(userId, request!, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
