namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common;
using User.Application.Services.Seller;

[ApiController]
[Route("api/v1/admin/seller-registrations")]
[RequireRole("admin", "staff")]
public sealed class AdminSellerRegistrationsController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<AdminSellerRegistrationsController> _logger;

    public AdminSellerRegistrationsController(ISellerService sellerService, ILogger<AdminSellerRegistrationsController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>Lists pending seller registration applications for review.</summary>
    [HttpGet]
    public async Task<IActionResult> ListSellerRegistrations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(page, pageSize);

        _logger.LogInformation("[REST ListSellerRegistrations] Fetching page {Page}, pageSize {PageSize}, status {Status}", safePage, safePageSize, status);
        var result = await _sellerService.GetSellerRegistrationsAsync(safePage, safePageSize, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>Approves a seller registration and assigns seller role.</summary>
    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> ApproveSellerRegistration(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ApproveSellerRegistrationDto? body,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var reviewerId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST ApproveSellerRegistration] Approving registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.ApproveSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }

    /// <summary>Rejects a seller registration with a required reason.</summary>
    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> RejectSellerRegistration(
        [FromRoute] Guid id,
        [FromBody] RejectSellerRegistrationDto body,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var reviewerId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST RejectSellerRegistration] Rejecting registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.RejectSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }
}
