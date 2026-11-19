using System.Text.Json;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadarUnitTests.ClientAdapters.Support;

namespace RoomToRentRadarUnitTests.ClientAdapters.Contracts;

/// <summary>T00.14 Phase 1: the shared contract enums must stay in sync with docs/api/openapi.yaml (names and order, values 1..N).</summary>
[TestFixture]
public class ContractEnumsOpenApiSyncTests
{
    private static readonly (Type EnumType, string SchemaName)[] ContractEnums =
    [
        (typeof(Amenity), "Amenity"),
        (typeof(SafetyFeature), "SafetyFeature"),
        (typeof(Furnishing), "Furnishing"),
        (typeof(WaterPriceUnit), "WaterPriceUnit"),
        (typeof(GuestPolicy), "GuestPolicy"),
        (typeof(CookingPolicy), "CookingPolicy"),
        (typeof(PhotoKind), "PhotoKind"),
        (typeof(TravelMode), "TravelMode"),
        (typeof(AlertState), "AlertState"),
        (typeof(ReportReason), "ReportReason"),
        (typeof(ReportStatus), "ReportStatus"),
        (typeof(ReportResolution), "ReportResolution"),
        (typeof(NotificationType), "NotificationType"),
        (typeof(NotificationTargetType), "NotificationTargetType"),
        (typeof(DevicePlatform), "DevicePlatform"),
        (typeof(ModerationQueue), "ModerationQueue")
    ];

    private static IEnumerable<TestCaseData> EnumCases() =>
        ContractEnums.Select(e => new TestCaseData(e.EnumType, e.SchemaName).SetName($"DomainEnum_Members_MatchOpenApiEnumValues({e.EnumType.Name})"));

    private static IEnumerable<TestCaseData> SerializationCases()
    {
        yield return new TestCaseData(Amenity.Wifi, "Wifi");
        yield return new TestCaseData(SafetyFeature.Cctv, "Cctv");
        yield return new TestCaseData(Furnishing.Full, "Full");
        yield return new TestCaseData(WaterPriceUnit.PerCubicMeter, "PerCubicMeter");
        yield return new TestCaseData(GuestPolicy.DaytimeOnly, "DaytimeOnly");
        yield return new TestCaseData(CookingPolicy.SharedKitchenOnly, "SharedKitchenOnly");
        yield return new TestCaseData(PhotoKind.Panorama, "Panorama");
        yield return new TestCaseData(TravelMode.Motorbike, "Motorbike");
        yield return new TestCaseData(AlertState.Off, "Off");
        yield return new TestCaseData(ReportReason.InappropriateContent, "InappropriateContent");
        yield return new TestCaseData(ReportStatus.ActionTaken, "ActionTaken");
        yield return new TestCaseData(ReportResolution.Dismissed, "Dismissed");
        yield return new TestCaseData(NotificationType.NewListingMatch, "NewListingMatch");
        yield return new TestCaseData(NotificationTargetType.SavedSearch, "SavedSearch");
        yield return new TestCaseData(DevicePlatform.iOS, "iOS");
        // ModerationQueue wire values are lowercase; the gateway lowercases, the converter writes the member name.
        yield return new TestCaseData(ModerationQueue.Pending, "Pending");
    }

    [TestCaseSource(nameof(EnumCases))]
    public void DomainEnum_Members_MatchOpenApiEnumValues(Type enumType, string schemaName)
    {
        var expected = ExpectedValues(schemaName);
        var actual = Enum.GetNames(enumType);
        if (enumType == typeof(ModerationQueue))
        {
            actual = actual.Select(name => name.ToLowerInvariant()).ToArray();
        }

        Assert.That(actual, Is.EqualTo(expected),
            $"Enum '{enumType.Name}' is out of sync with '{schemaName}' in docs/api/openapi.yaml (member names and order must match 1:1).");
    }

    [Test]
    public void DomainEnums_HaveExplicitUniqueValuesStartingAtOne()
    {
        Assert.Multiple(() =>
        {
            foreach (var (enumType, _) in ContractEnums)
            {
                var values = Enum.GetValues(enumType).Cast<int>().ToArray();
                Assert.That(values, Is.EqualTo(Enumerable.Range(1, values.Length)),
                    $"Enum '{enumType.Name}' must declare explicit unique values 1..N in openapi order.");
            }
        });
    }

    [TestCaseSource(nameof(SerializationCases))]
    public void Enum_SerializesAsString_WithApiJsonOptions(object value, string expectedName)
    {
        var json = JsonSerializer.Serialize(value, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo($"\"{expectedName}\""), "Enums must serialize as their openapi string name, not as numbers.");
            Assert.That(JsonSerializer.Deserialize(json, value.GetType(), ApiResponseReader.JsonOptions), Is.EqualTo(value));
        });
    }

    private static string[] ExpectedValues(string schemaName) => schemaName switch
    {
        // Inline enum of ResolveReportRequest.resolution, not a standalone schema.
        "ReportResolution" => OpenApiSpec.PropertyEnumValues("ResolveReportRequest", "resolution").ToArray(),
        // Inline enum of NotificationDto.targetType; wire 'null' means no target and is not an enum member.
        "NotificationTargetType" => OpenApiSpec.PropertyEnumValues("NotificationDto", "targetType").Where(v => v != "null").ToArray(),
        // Inline lowercase query parameter enum of listModerationRooms.
        "ModerationQueue" => OpenApiSpec.OperationParameterEnumValues("listModerationRooms", "queue").ToArray(),
        _ => OpenApiSpec.EnumValues(schemaName).ToArray()
    };
}