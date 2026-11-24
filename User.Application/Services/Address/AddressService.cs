using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using User.Domain.Exceptions;
using User.Application.Common;
using User.Application.Interfaces;
using User.Domain.Entities;

namespace User.Application.Services.Address;

public sealed class AddressService : IAddressService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<AddressService> _logger;
    private readonly IFulfillmentServiceClient _fulfillment;

    public AddressService(IUserDbContext dbContext, ILogger<AddressService> logger, IFulfillmentServiceClient fulfillment)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fulfillment = fulfillment ?? throw new ArgumentNullException(nameof(fulfillment));
    }

    public async Task<IReadOnlyList<UserAddressDto>> GetAddressesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(a => ToDto(a))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserAddressDto?> GetDefaultAddressAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var address = await _dbContext.UserAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault && !a.IsDeleted, cancellationToken);

        return address is null ? null : ToDto(address);
    }

    public async Task<UserAddressDto> CreateAddressAsync(Guid userId, CreateAddressRequest request, CancellationToken cancellationToken = default)
    {
        ValidateFields(request.RecipientName, request.Phone, request.ProvinceId, request.DistrictId, request.WardCode, request.AddressLine);
        await LocationVerification.EnsureConfirmedAsync(_fulfillment, request.ProvinceId, request.DistrictId, request.WardCode.Trim(), cancellationToken);

        await using var tx = await BeginRelationalTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var existing = await _dbContext.UserAddresses
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        var makeDefault = request.IsDefault || existing.Count == 0;
        if (makeDefault)
        {
            ClearDefaultFlags(existing, now);
        }

        var entity = new UserAddress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RecipientName = request.RecipientName.Trim(),
            Phone = request.Phone.Trim(),
            Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
            ProvinceId = request.ProvinceId,
            ProvinceName = request.ProvinceName.Trim(),
            DistrictId = request.DistrictId,
            DistrictName = request.DistrictName.Trim(),
            WardCode = request.WardCode.Trim(),
            WardName = request.WardName.Trim(),
            AddressLine = request.AddressLine.Trim(),
            IsDefault = makeDefault,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        _dbContext.UserAddresses.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("[ADDRESS] Created address '{AddressId}' for UserId '{UserId}' (IsDefault={IsDefault}).", entity.Id, userId, entity.IsDefault);
        return ToDto(entity);
    }

    public async Task<UserAddressDto> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressRequest request, CancellationToken cancellationToken = default)
    {
        ValidateFields(request.RecipientName, request.Phone, request.ProvinceId, request.DistrictId, request.WardCode, request.AddressLine);

        await using var tx = await BeginRelationalTransactionAsync(cancellationToken);
        var entity = await FindOwnedAddressAsync(userId, addressId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // Only a changed chain is checked: a new recipient name or phone must not wait for the GHN lists.
        if (entity.ProvinceId != request.ProvinceId || entity.DistrictId != request.DistrictId
            || !string.Equals(entity.WardCode, request.WardCode.Trim(), StringComparison.Ordinal))
        {
            await LocationVerification.EnsureConfirmedAsync(_fulfillment, request.ProvinceId, request.DistrictId, request.WardCode.Trim(), cancellationToken);
        }

        if (request.IsDefault && !entity.IsDefault)
        {
            var others = await _dbContext.UserAddresses
                .Where(a => a.UserId == userId && a.Id != addressId && a.IsDefault && !a.IsDeleted)
                .ToListAsync(cancellationToken);
            ClearDefaultFlags(others, now);
            entity.IsDefault = true;
        }

        entity.RecipientName = request.RecipientName.Trim();
        entity.Phone = request.Phone.Trim();
        entity.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
        entity.ProvinceId = request.ProvinceId;
        entity.ProvinceName = request.ProvinceName.Trim();
        entity.DistrictId = request.DistrictId;
        entity.DistrictName = request.DistrictName.Trim();
        entity.WardCode = request.WardCode.Trim();
        entity.WardName = request.WardName.Trim();
        entity.AddressLine = request.AddressLine.Trim();
        entity.UpdatedAt = now;
        entity.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<UserAddressDto> SetDefaultAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
    {
        await using var tx = await BeginRelationalTransactionAsync(cancellationToken);
        var entity = await FindOwnedAddressAsync(userId, addressId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var others = await _dbContext.UserAddresses
            .Where(a => a.UserId == userId && a.Id != addressId && a.IsDefault && !a.IsDeleted)
            .ToListAsync(cancellationToken);
        ClearDefaultFlags(others, now);

        entity.IsDefault = true;
        entity.UpdatedAt = now;
        entity.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task DeleteAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
    {
        await using var tx = await BeginRelationalTransactionAsync(cancellationToken);
        var entity = await FindOwnedAddressAsync(userId, addressId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var wasDefault = entity.IsDefault;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.IsDefault = false;
        entity.UpdatedAt = now;
        entity.UpdatedBy = userId;

        if (wasDefault)
        {
            var nextDefault = await _dbContext.UserAddresses
                .Where(a => a.UserId == userId && a.Id != addressId && !a.IsDeleted)
                .OrderByDescending(a => a.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextDefault is not null)
            {
                nextDefault.IsDefault = true;
                nextDefault.UpdatedAt = now;
                nextDefault.UpdatedBy = userId;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
    }

    private async Task<UserAddress> FindOwnedAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.UserAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && !a.IsDeleted, cancellationToken);

        if (entity is null)
        {
            throw new DomainException("ADDRESS_NOT_FOUND", "Address not found with the specified ID.", 404);
        }

        if (entity.UserId != userId)
        {
            throw new DomainException("FORBIDDEN", "You do not have permission to access this address.", 403);
        }

        return entity;
    }

    private async Task<IDbContextTransaction?> BeginRelationalTransactionAsync(CancellationToken cancellationToken)
    {
        return !string.Equals(_dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase)
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private static void ClearDefaultFlags(IEnumerable<UserAddress> addresses, DateTimeOffset now)
    {
        foreach (var addr in addresses)
        {
            if (addr.IsDefault)
            {
                addr.IsDefault = false;
                addr.UpdatedAt = now;
            }
        }
    }

    private static void ValidateFields(string recipientName, string phone, int provinceId, int districtId, string wardCode, string addressLine)
    {
        if (string.IsNullOrWhiteSpace(recipientName) || string.IsNullOrWhiteSpace(phone) ||
            provinceId <= 0 || districtId <= 0 || string.IsNullOrWhiteSpace(wardCode) ||
            string.IsNullOrWhiteSpace(addressLine))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Address fields and GHN location identifiers are required.");
        }
    }

    private static UserAddressDto ToDto(UserAddress a) => new(
        a.Id, a.UserId, a.RecipientName, a.Phone, a.Label,
        a.ProvinceId, a.ProvinceName, a.DistrictId, a.DistrictName,
        a.WardCode, a.WardName, a.AddressLine, a.IsDefault,
        a.CreatedAt, a.UpdatedAt);
}
