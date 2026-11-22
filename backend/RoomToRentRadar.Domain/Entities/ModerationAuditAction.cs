namespace RoomToRentRadar.Domain.Entities;

public enum ModerationAuditAction
{
    Claim,
    Release,
    Resolve,
    Approve
}

/// <summary>Wire values for <see cref="ModerationAuditAction"/>.</summary>
public static class ModerationAuditActionWire
{
    public const string Claim = "claim";
    public const string Release = "release";
    public const string Resolve = "resolve";
    public const string Approve = "approve";

    public static readonly IReadOnlyList<string> All = [Claim, Release, Resolve, Approve];

    public static bool TryParse(string? value, out ModerationAuditAction action)
    {
        switch (value)
        {
            case Claim:
                action = ModerationAuditAction.Claim;
                return true;
            case Release:
                action = ModerationAuditAction.Release;
                return true;
            case Resolve:
                action = ModerationAuditAction.Resolve;
                return true;
            case Approve:
                action = ModerationAuditAction.Approve;
                return true;
            default:
                action = default;
                return false;
        }
    }

    public static string ToWireValue(ModerationAuditAction action) => action switch
    {
        ModerationAuditAction.Claim => Claim,
        ModerationAuditAction.Release => Release,
        ModerationAuditAction.Resolve => Resolve,
        ModerationAuditAction.Approve => Approve,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static ModerationAuditAction FromWireValue(string value)
        => TryParse(value, out var action)
            ? action
            : throw new InvalidOperationException($"Unknown moderation audit action '{value}'.");
}
