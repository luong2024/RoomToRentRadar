using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing structured house rules and building regulations for a room (US11 AC1).
/// </summary>
public sealed record HouseRulesPolicy
{
    public const int MaxQuietHoursLength = 200;
    public const int MaxRuleSectionLength = 300;
    public const int MaxAdditionalRulesCount = 10;
    public const int MaxAdditionalRuleItemLength = 200;

    public string QuietHours { get; }
    public string GuestPolicy { get; }
    public string CookingRules { get; }
    public string ParkingRules { get; }
    public IReadOnlyList<string> AdditionalRules { get; }

    private HouseRulesPolicy(
        string quietHours,
        string guestPolicy,
        string cookingRules,
        string parkingRules,
        IReadOnlyList<string> additionalRules)
    {
        QuietHours = quietHours;
        GuestPolicy = guestPolicy;
        CookingRules = cookingRules;
        ParkingRules = parkingRules;
        AdditionalRules = additionalRules;
    }

    public static HouseRulesPolicy Create(
        string? quietHours,
        string? guestPolicy,
        string? cookingRules,
        string? parkingRules,
        IEnumerable<string>? additionalRules = null)
    {
        var normalizedQuietHours = NormalizeRequired(quietHours, nameof(QuietHours), MaxQuietHoursLength);
        var normalizedGuestPolicy = NormalizeRequired(guestPolicy, nameof(GuestPolicy), MaxRuleSectionLength);
        var normalizedCookingRules = NormalizeRequired(cookingRules, nameof(CookingRules), MaxRuleSectionLength);
        var normalizedParkingRules = NormalizeRequired(parkingRules, nameof(ParkingRules), MaxRuleSectionLength);

        var normalizedAdditional = new List<string>();
        if (additionalRules is not null)
        {
            foreach (var raw in additionalRules)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var trimmed = raw.Trim();
                if (trimmed.Length > MaxAdditionalRuleItemLength)
                {
                    throw new HouseRulesValidationException(
                        $"Each additional rule must not exceed {MaxAdditionalRuleItemLength} characters.");
                }

                normalizedAdditional.Add(trimmed);
                if (normalizedAdditional.Count > MaxAdditionalRulesCount)
                {
                    throw new HouseRulesValidationException(
                        $"A maximum of {MaxAdditionalRulesCount} additional rules is allowed.");
                }
            }
        }

        return new HouseRulesPolicy(
            normalizedQuietHours,
            normalizedGuestPolicy,
            normalizedCookingRules,
            normalizedParkingRules,
            normalizedAdditional.AsReadOnly());
    }

    public IReadOnlyList<string> ToSummaryLines()
    {
        var lines = new List<string>(4 + AdditionalRules.Count)
        {
            $"Quiet hours: {QuietHours}",
            $"Guest policy: {GuestPolicy}",
            $"Cooking rules: {CookingRules}",
            $"Parking rules: {ParkingRules}"
        };

        lines.AddRange(AdditionalRules);
        return lines;
    }

    public bool HasSameRules(HouseRulesPolicy? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(QuietHours, other.QuietHours, StringComparison.Ordinal)
            && string.Equals(GuestPolicy, other.GuestPolicy, StringComparison.Ordinal)
            && string.Equals(CookingRules, other.CookingRules, StringComparison.Ordinal)
            && string.Equals(ParkingRules, other.ParkingRules, StringComparison.Ordinal)
            && AdditionalRules.SequenceEqual(other.AdditionalRules, StringComparer.Ordinal);
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new HouseRulesValidationException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new HouseRulesValidationException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
