namespace RoomToRentRadar.Application.Ports.Outbound;

public sealed record VerifiedGoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified);

/// <summary>
/// Outbound port for verifying Google OpenID Connect ID tokens on the server.
/// </summary>
public interface IGoogleIdentityVerifier
{
    Task<VerifiedGoogleIdentity> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
