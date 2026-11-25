namespace RoomToRentRadar.Models.RoomDetails;

public sealed class RoomHouseRulesApiDto
{
    public string RoomId { get; set; } = string.Empty;
    public Guid? LandlordId { get; set; }
    public int Version { get; set; }
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;
    public List<string> AdditionalRules { get; set; } = [];
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public bool IsLegacyFallback { get; set; }
}

public sealed class HouseRulesVersionApiDto
{
    public string RoomId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;
    public List<string> AdditionalRules { get; set; } = [];
    public string? ChangeNote { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid UpdatedByUserId { get; set; }
}

public sealed record UpdateHouseRulesApiRequest(
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules,
    List<string>? AdditionalRules = null,
    string? ChangeNote = null,
    int? ExpectedVersion = null);

public sealed class RoomHouseRulesModel
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public string RoomId { get; set; } = string.Empty;
    public Guid? LandlordId { get; set; }
    public int Version { get; set; }
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;
    public List<string> AdditionalRules { get; set; } = [];
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public bool IsLegacyFallback { get; set; }

    public string VersionBadgeText => IsLegacyFallback ? "Legacy" : $"v{Version}";

    public string NaturalStatusText => IsLegacyFallback
        ? "Standard rules"
        : UpdatedAtUtc.HasValue
            ? $"Updated {UpdatedAtUtc.Value.ToOffset(VietnamOffset):dd/MM/yyyy}"
            : "Active rules";

    public bool HasAdditionalRules => AdditionalRules.Count > 0;

    public string FormattedUpdatedAt => UpdatedAtUtc.HasValue
        ? $"Updated {UpdatedAtUtc.Value.ToOffset(VietnamOffset):dd/MM/yyyy HH:mm} (ICT)"
        : string.Empty;
}

public sealed class HouseRulesVersionModel
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public string RoomId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;
    public List<string> AdditionalRules { get; set; } = [];
    public string? ChangeNote { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid UpdatedByUserId { get; set; }

    public string VersionBadgeText => $"v{Version}";

    public string EditionLabel => $"Update #{Version}";

    public bool HasChangeNote => !string.IsNullOrWhiteSpace(ChangeNote);

    public bool HasAdditionalRules => AdditionalRules.Count > 0;

    public string FormattedUpdatedAt => $"{UpdatedAtUtc.ToOffset(VietnamOffset):dd/MM/yyyy HH:mm} (ICT)";
}
