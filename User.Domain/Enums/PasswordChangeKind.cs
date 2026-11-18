namespace User.Domain.Enums;

/// <summary>
/// Distinguishes user-initiated password change from OTP-based password reset.
/// </summary>
public enum PasswordChangeKind
{
    Changed = 1,
    Reset = 2
}
