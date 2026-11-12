namespace RoomToRentRadar.Adapters.Outbound.Authentication;

/// <summary>
/// Configuration options for verifying Google OAuth2 / OIDC ID tokens.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>
    /// Expected Google OAuth 2.0 Client ID (Audience).
    /// </summary>
    public string ClientId { get; init; } = string.Empty;
}
