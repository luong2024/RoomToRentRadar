using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record HouseRulesInput(
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules);

public static class HouseRulesValidator
{
    public const int MaxFieldLength = 300;

    public static bool TryValidate(
        UpdateHouseRulesRequest request,
        out HouseRulesInput input,
        out IReadOnlyDictionary<string, List<string>> errors)
    {
        var fieldErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var quietHours = NormalizeField("quiet_hours", request.QuietHours, fieldErrors);
        var guestPolicy = NormalizeField("guest_policy", request.GuestPolicy, fieldErrors);
        var cookingRules = NormalizeField("cooking_rules", request.CookingRules, fieldErrors);
        var parkingRules = NormalizeField("parking_rules", request.ParkingRules, fieldErrors);

        input = new HouseRulesInput(quietHours, guestPolicy, cookingRules, parkingRules);
        errors = fieldErrors;
        return fieldErrors.Count == 0;
    }

    private static string NormalizeField(
        string key,
        string? value,
        Dictionary<string, List<string>> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            AddError(key, "This field is required.", errors);
        }
        else if (trimmed.Length > MaxFieldLength)
        {
            AddError(key, $"This field must be at most {MaxFieldLength} characters.", errors);
        }

        return trimmed;
    }

    private static void AddError(
        string key,
        string message,
        Dictionary<string, List<string>> errors)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }
}
