namespace GreenHerb.Application.Abstractions.Auth;

public sealed record GoogleTokenVerificationResult(
    GoogleIdentityInfo? Identity,
    string? FailureReason)
{
    public bool IsSuccess => Identity is not null;

    public static GoogleTokenVerificationResult Success(GoogleIdentityInfo identity) =>
        new(identity, null);

    public static GoogleTokenVerificationResult Failure(string reason) =>
        new(null, reason);
}

public interface IGoogleTokenVerifier
{
    Task<GoogleTokenVerificationResult> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}
