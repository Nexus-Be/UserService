namespace User.Domain.Constants;

using System;
using User.Domain.Enums;

public static class ReputationRules
{
    public const int MinScore = 0;
    public const int MaxScore = 100;
    public const int DefaultInitialScore = 50;

    public const int DefaultMinScoreToBid = 40;
    public const int DefaultMinScoreToSell = 50;

    public const int PaymentTimeoutPenalty = -10;
    public const int SellerCancelPenalty = -5;
    public const int OrderCompletedReward = 1;

    public const int AntiFarmingCooldownDays = 7;

    public static int ClampScore(int currentScore, int delta)
    {
        return Math.Clamp(currentScore + delta, MinScore, MaxScore);
    }

    public static TrustLevel CalculateTrustLevel(int score)
    {
        var clamped = Math.Clamp(score, MinScore, MaxScore);
        return clamped switch
        {
            >= 90 => TrustLevel.Excellent,
            >= 70 => TrustLevel.Good,
            >= 50 => TrustLevel.Standard,
            >= 40 => TrustLevel.Probation,
            _ => TrustLevel.Untrusted
        };
    }

    public static int? GetNextLevelThreshold(int score)
    {
        return score switch
        {
            < 40 => 40,
            < 50 => 50,
            < 70 => 70,
            < 90 => 90,
            _ => null
        };
    }

    public static int GetReviewScoreDelta(int rating)
    {
        return rating switch
        {
            1 => -2,
            2 => -1,
            3 => 0,
            4 or 5 => 1,
            _ => 0
        };
    }
}
