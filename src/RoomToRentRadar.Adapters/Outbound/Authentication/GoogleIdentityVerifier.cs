using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Adapters.Outbound.Authentication;

/// <summary>
/// Server-side Google OpenID Connect ID token verifier.
/// Validates cryptographic signature, issuer, audience (client ID), lifetime, subject, and email_verified claims.
/// </summary>
public sealed class GoogleIdentityVerifier : IGoogleIdentityVerifier
{
    public const string GoogleDiscoveryEndpoint = "https://accounts.google.com/.well-known/openid-configuration";

    private static readonly string[] ValidGoogleIssuers =
    [
        "https://accounts.google.com",
        "accounts.google.com"
    ];

    private readonly IOptions<GoogleAuthOptions> _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly TimeProvider _timeProvider;

    public GoogleIdentityVerifier(
        IOptions<GoogleAuthOptions> options,
        IConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _configurationManager = configurationManager ?? new ConfigurationManager<OpenIdConnectConfiguration>(
            GoogleDiscoveryEndpoint,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<VerifiedGoogleIdentity> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidGoogleTokenException("Google ID token cannot be empty.");
        }

        var clientId = _options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidGoogleTokenException("Google authentication client ID is not configured.");
        }

        OpenIdConnectConfiguration openIdConfig;
        try
        {
            openIdConfig = await _configurationManager.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidGoogleTokenException("Unable to retrieve Google signing keys.", ex);
        }

        var handler = new JsonWebTokenHandler
        {
            MapInboundClaims = false
        };

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = openIdConfig.SigningKeys,
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuers = ValidGoogleIssuers,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        var validationResult = await handler.ValidateTokenAsync(idToken, validationParameters);
        if (!validationResult.IsValid || validationResult.ClaimsIdentity is null)
        {
            throw new InvalidGoogleTokenException(
                "Google ID token validation failed.",
                validationResult.Exception ?? new SecurityTokenValidationException("Token validation failed."));
        }

        var identity = validationResult.ClaimsIdentity;

        var subject = identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var email = identity.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? identity.FindFirst(ClaimTypes.Email)?.Value;

        var emailVerifiedRaw = identity.FindFirst("email_verified")?.Value;
        var emailVerified = string.Equals(emailVerifiedRaw, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidGoogleTokenException("Google ID token is missing the 'sub' claim.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidGoogleTokenException("Google ID token is missing the 'email' claim.");
        }

        if (!emailVerified)
        {
            throw new InvalidGoogleTokenException("Google account email address is not verified.");
        }

        return new VerifiedGoogleIdentity(subject, email, emailVerified);
    }
}
