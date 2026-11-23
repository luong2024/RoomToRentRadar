using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class RoomHouseRulesDocument
{
    public const string CurrentDocumentType = "current";
    public const string RevisionDocumentType = "revision";

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonProperty("documentType")]
    public string DocumentType { get; set; } = CurrentDocumentType;

    [JsonProperty("landlordId")]
    public Guid? LandlordId { get; set; }

    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("quietHours")]
    public string QuietHours { get; set; } = string.Empty;

    [JsonProperty("guestPolicy")]
    public string GuestPolicy { get; set; } = string.Empty;

    [JsonProperty("cookingRules")]
    public string CookingRules { get; set; } = string.Empty;

    [JsonProperty("parkingRules")]
    public string ParkingRules { get; set; } = string.Empty;

    [JsonProperty("additionalRules")]
    public List<string> AdditionalRules { get; set; } = [];

    [JsonProperty("changeNote")]
    public string? ChangeNote { get; set; }

    [JsonProperty("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [JsonProperty("updatedByUserId")]
    public Guid UpdatedByUserId { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    public static RoomHouseRulesDocument FromCurrentDomain(RoomHouseRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new RoomHouseRulesDocument
        {
            Id = rules.Id,
            RoomId = rules.RoomId,
            DocumentType = CurrentDocumentType,
            LandlordId = rules.LandlordId,
            Version = rules.CurrentVersion,
            QuietHours = rules.CurrentPolicy.QuietHours,
            GuestPolicy = rules.CurrentPolicy.GuestPolicy,
            CookingRules = rules.CurrentPolicy.CookingRules,
            ParkingRules = rules.CurrentPolicy.ParkingRules,
            AdditionalRules = rules.CurrentPolicy.AdditionalRules.ToList(),
            UpdatedAtUtc = rules.UpdatedAtUtc,
            UpdatedByUserId = rules.UpdatedByUserId,
            ETag = rules.ETag
        };
    }

    public static RoomHouseRulesDocument FromRevisionDomain(HouseRulesRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return new RoomHouseRulesDocument
        {
            Id = $"{RoomHouseRules.BuildId(revision.RoomId)}:v{revision.Version}",
            RoomId = revision.RoomId,
            DocumentType = RevisionDocumentType,
            Version = revision.Version,
            QuietHours = revision.Policy.QuietHours,
            GuestPolicy = revision.Policy.GuestPolicy,
            CookingRules = revision.Policy.CookingRules,
            ParkingRules = revision.Policy.ParkingRules,
            AdditionalRules = revision.Policy.AdditionalRules.ToList(),
            ChangeNote = revision.ChangeNote,
            UpdatedAtUtc = revision.UpdatedAtUtc,
            UpdatedByUserId = revision.UpdatedByUserId
        };
    }

    public RoomHouseRules ToCurrentDomain()
    {
        var policy = HouseRulesPolicy.Create(
            QuietHours,
            GuestPolicy,
            CookingRules,
            ParkingRules,
            AdditionalRules);

        return RoomHouseRules.Rehydrate(
            Id,
            RoomId,
            LandlordId,
            Version,
            policy,
            UpdatedAtUtc,
            UpdatedByUserId,
            ETag);
    }

    public HouseRulesRevision ToRevisionDomain()
    {
        var policy = HouseRulesPolicy.Create(
            QuietHours,
            GuestPolicy,
            CookingRules,
            ParkingRules,
            AdditionalRules);

        return new HouseRulesRevision(
            RoomId,
            Version,
            policy,
            ChangeNote,
            UpdatedByUserId,
            UpdatedAtUtc);
    }
}
