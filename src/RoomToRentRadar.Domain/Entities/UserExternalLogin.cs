using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Represents an external identity provider login (e.g., Google) linked to a <see cref="User"/> aggregate.
/// Identified uniquely by (Provider, ProviderSubject) rather than email alone.
/// </summary>
public sealed class UserExternalLogin
{
    public const string GoogleProvider = "GOOGLE";

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderSubject { get; private set; } = string.Empty;

    public EmailAddress ProviderEmail { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    private UserExternalLogin()
    {
    }

    internal static UserExternalLogin Create(
        Guid userId,
        string provider,
        string providerSubject,
        EmailAddress providerEmail,
        DateTimeOffset utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidUserStateException("External login requires a valid user identifier.");
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidUserStateException("External identity provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(providerSubject))
        {
            throw new InvalidUserStateException("External identity provider subject is required.");
        }

        ArgumentNullException.ThrowIfNull(providerEmail);

        return new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider.Trim().ToUpperInvariant(),
            ProviderSubject = providerSubject.Trim(),
            ProviderEmail = providerEmail,
            CreatedAt = utcNow
        };
    }
}
