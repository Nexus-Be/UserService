namespace User.Domain.Enums;

public enum AuctionRestrictionType
{
    None = 0,
    TempRestricted = 1,
    Suspended = 2
}

public enum AuctionViolationType
{
    WinnerNonPayment = 0,
    SecondChanceNonPayment = 1
}

public enum AuctionViolationSource
{
    System = 0,
    Admin = 1
}

public enum AuctionSanctionType
{
    None = 0,
    Warning = 1,
    DepositRateIncreased = 2,
    TempRestricted = 3,
    Suspended = 4
}

public enum AuctionAdminAdjustAction
{
    ResetStreak = 0,
    SetStreak = 1,
    ApplyRestriction = 2,
    LiftRestriction = 3,
    Suspend = 4
}
