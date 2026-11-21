namespace User.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;

public sealed record FirebaseUserClaims(
    string Uid,
    string Email,
    bool EmailVerified,
    string? DisplayName,
    string? GivenName,
    string? FamilyName,
    string? PictureUrl);

public interface IFirebaseAuthService
{
    Task<FirebaseUserClaims> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}
