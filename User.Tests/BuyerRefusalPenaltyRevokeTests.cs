namespace User.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Internal;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// The withdrawal of a buyer refusal penalty: it gives back what the penalty really took, once per sub-order, and a penalty that arrives
/// after the withdrawal is not applied.
/// </summary>
public sealed class BuyerRefusalPenaltyRevokeTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly InternalUserService _service;
    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();

    public BuyerRefusalPenaltyRevokeTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"revoke_{Guid.NewGuid():N}").Options);
        _service = new InternalUserService(_db, _cache, NullLogger<InternalUserService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    private async Task SeedBuyerAsync(int score)
    {
        _db.Users.Add(new User { Id = _buyerId, Email = $"b{Guid.NewGuid():N}@nexus.local", PasswordHash = "hash", FirstName = "B", LastName = "Buyer", Status = UserStatus.Active });
        _db.ReputationScores.Add(new ReputationScore { Id = Guid.NewGuid(), UserId = _buyerId, CurrentScore = score, TrustLevel = TrustLevel.Standard, SellerScore = 100, SellerTrustLevel = TrustLevel.Good });
        await _db.SaveChangesAsync();
    }

    private Task<InternalReputationAdjustResponse> PenaltyAsync(string subOrderId, int delta = -5) =>
        _service.ApplyInternalReputationAdjustAsync(new InternalReputationAdjustRequest
        {
            UserId = _buyerId,
            TargetRole = TargetRole.Buyer,
            PartnerUserId = _sellerId,
            ScoreDelta = delta,
            Reason = ReputationChangeReason.BuyerRefusedDelivery,
            ReferenceType = ReferenceType.Order,
            ReferenceId = subOrderId,
            Note = "refused",
        });

    private Task<InternalPenaltyRevokeResponse> RevokeAsync(string subOrderId, string note = "The buyer was not at fault, the dispute was upheld.") =>
        _service.RevokeBuyerRefusalPenaltyAsync(new InternalPenaltyRevokeRequest { UserId = _buyerId, ReferenceId = subOrderId, ApprovedBy = _admin, Note = note });

    private async Task<int> ScoreAsync() => (await _db.ReputationScores.AsNoTracking().FirstAsync(s => s.UserId == _buyerId)).CurrentScore;

    private int RevokeLogs() => _db.ReputationLogs.Count(l => l.Reason == ReputationChangeReason.BuyerRefusalPenaltyRevoked);

    [Fact]
    public async Task A_penalty_that_was_applied_is_given_back_exactly_once()
    {
        await SeedBuyerAsync(80);
        Assert.Equal(-5, (await PenaltyAsync("sub-1")).AppliedDelta);
        Assert.Equal(75, await ScoreAsync());

        var first = await RevokeAsync("sub-1");
        var second = await RevokeAsync("sub-1");

        Assert.Equal("Restored", first.Outcome);
        Assert.Equal(5, first.RestoredPoints);
        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(5, second.RestoredPoints);
        Assert.Equal(80, await ScoreAsync());
        Assert.Equal(1, RevokeLogs());
    }

    [Fact]
    public async Task Only_the_points_really_taken_are_given_back_when_the_score_was_near_zero()
    {
        await SeedBuyerAsync(2);
        Assert.Equal(-2, (await PenaltyAsync("sub-low")).AppliedDelta);        // limited by the 0 floor, not the configured 5

        var result = await RevokeAsync("sub-low");

        Assert.Equal(2, result.RestoredPoints);
        Assert.Equal(2, await ScoreAsync());
    }

    [Fact]
    public async Task A_penalty_that_took_nothing_has_nothing_to_give_back()
    {
        await SeedBuyerAsync(0);
        Assert.Equal(0, (await PenaltyAsync("sub-zero")).AppliedDelta);        // no log is written when nothing changed

        var result = await RevokeAsync("sub-zero");

        Assert.Equal("NothingToRestore", result.Outcome);
        Assert.Equal(0, result.RestoredPoints);
        Assert.Equal(0, await ScoreAsync());
    }

    [Fact]
    public async Task A_withdrawal_that_comes_first_blocks_a_penalty_that_arrives_later()
    {
        await SeedBuyerAsync(80);

        var revoked = await RevokeAsync("sub-race");
        var late = await PenaltyAsync("sub-race");

        Assert.Equal("NothingToRestore", revoked.Outcome);
        Assert.True(late.IsDuplicate);
        Assert.Equal(0, late.AppliedDelta);
        Assert.Equal(80, await ScoreAsync());
        Assert.Equal(0, _db.ReputationLogs.Count(l => l.Reason == ReputationChangeReason.BuyerRefusedDelivery));
    }

    [Fact]
    public async Task A_withdrawal_does_not_touch_other_sub_orders_of_the_same_seller()
    {
        await SeedBuyerAsync(80);
        await PenaltyAsync("sub-a");
        await PenaltyAsync("sub-b");                                            // 70 now, two penalties against the same seller

        await RevokeAsync("sub-a");

        Assert.Equal(75, await ScoreAsync());                                  // only sub-a came back
        Assert.Equal(1, RevokeLogs());
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_withdrawal_needs_a_reason_of_at_least_ten_characters(string note)
    {
        await SeedBuyerAsync(80);
        await PenaltyAsync("sub-1");

        var error = await Assert.ThrowsAsync<DomainException>(() => RevokeAsync("sub-1", note));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal(75, await ScoreAsync());
    }

    [Fact]
    public async Task A_withdrawal_for_an_unknown_user_is_404()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _service.RevokeBuyerRefusalPenaltyAsync(
            new InternalPenaltyRevokeRequest { UserId = Guid.NewGuid(), ReferenceId = "x", ApprovedBy = _admin, Note = "A long enough reason." }));

        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task Other_reputation_changes_on_the_same_sub_order_are_not_blocked_by_a_withdrawal()
    {
        await SeedBuyerAsync(80);
        await RevokeAsync("sub-1");

        var other = await _service.ApplyInternalReputationAdjustAsync(new InternalReputationAdjustRequest
        {
            UserId = _buyerId, TargetRole = TargetRole.Buyer, ScoreDelta = -3, Reason = ReputationChangeReason.ViolationPenalty,
            ReferenceType = ReferenceType.Order, ReferenceId = "sub-1", Note = "different reason, same sub-order",
        });

        Assert.False(other.IsDuplicate);
        Assert.Equal(-3, other.AppliedDelta);
    }
}
