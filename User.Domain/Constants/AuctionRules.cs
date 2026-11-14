namespace User.Domain.Constants;

using System;
using User.Domain.Enums;

public static class AuctionRules
{
    public const int TempRestrictionDays = 7;

    public static int GetStreakReputationDelta(int newStreak) => newStreak switch
    {
        <= 0 => 0,
        1 => -5,
        2 => -10,
        3 => -15,
        _ => -20
    };

    public static (AuctionSanctionType SanctionType, DateTimeOffset? SanctionUntil) GetStreakSanction(int newStreak, DateTimeOffset now) => newStreak switch
    {
        <= 0 => (AuctionSanctionType.None, null),
        1 => (AuctionSanctionType.Warning, null),
        2 => (AuctionSanctionType.DepositRateIncreased, null),
        3 => (AuctionSanctionType.TempRestricted, now.AddDays(TempRestrictionDays)),
        _ => (AuctionSanctionType.Suspended, null)
    };
}
