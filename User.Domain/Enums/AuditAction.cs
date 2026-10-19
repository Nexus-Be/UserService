namespace User.Domain.Enums;

public enum AuditAction
{
    UserUpdate = 0,
    UserBan = 1,
    UserUnban = 2,
    SellerApprove = 3,
    SellerReject = 4,
    RoleAssign = 5,
    SoftDelete = 6,
    ReputationAdjust = 7,
    PasswordReset = 8,
    SystemConfigUpdate = 9
}
