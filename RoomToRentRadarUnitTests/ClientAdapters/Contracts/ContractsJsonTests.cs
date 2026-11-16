using System.Text.Json;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Notifications;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Ports.Contracts.Responses.Chat;
using RoomToRentRadar.Ports.Contracts.Responses.Notifications;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.SavedSearches;

namespace RoomToRentRadarUnitTests.ClientAdapters.Contracts;

/// <summary>T00.14 Phases 2-3: contract DTOs round-trip through the shared API JSON options (camelCase, enum strings, DateOnly).</summary>
[TestFixture]
public class ContractsJsonTests
{
    [Test]
    public void RoomDetailDto_Deserializes_EnumStrings_DateOnly_NullHouseRules()
    {
        const string json = """
        {
          "id": "seed-z115",
          "title": "Student room Z115",
          "description": "Near the campus gate",
          "roomType": "BoardingRoom",
          "status": "Available",
          "isVerified": true,
          "monthlyRentVnd": 1500000,
          "depositVnd": 1500000,
          "areaM2": 20.5,
          "maxOccupants": 2,
          "furnishing": "Full",
          "availableFrom": "2026-01-15",
          "address": { "addressLine": "12 Z115", "ward": "Ward 1", "district": "District 1" },
          "location": { "latitude": 21.5851, "longitude": 105.8066 },
          "distanceKm": 0.3,
          "utilities": {
            "electricityPriceVndPerKwh": 3500,
            "waterPriceVnd": 25000,
            "waterPriceUnit": "PerCubicMeter",
            "internetFeeVnd": 100000,
            "parkingFeeVnd": 50000,
            "otherFeesNote": null,
            "estimatedMonthlyTotalVnd": 1800000
          },
          "amenities": ["Wifi", "AirConditioner", "PrivateBathroom"],
          "safety": { "score": 4, "isVerified": true, "features": ["Cctv", "SecureEntrance"] },
          "houseRules": null,
          "photos": [
            { "id": "p1", "kind": "Standard", "thumbUrl": "https://cdn.example/t.jpg", "mediumUrl": "https://cdn.example/m.jpg", "originalUrl": "https://cdn.example/o.jpg", "width": 800, "height": 600, "sortOrder": 0 }
          ],
          "landlord": { "id": "u1", "displayName": "Landlord A", "isVerified": true, "memberSinceUtc": "2025-01-01T00:00:00Z", "phoneNumber": "0912345678", "zaloUrl": "https://zalo.me/0912345678" },
          "isFavourite": false,
          "publishedAtUtc": "2025-12-01T08:00:00Z",
          "lastUpdatedAtUtc": "2025-12-10T08:00:00Z"
        }
        """;

        var dto = JsonSerializer.Deserialize<RoomDetailDto>(json, ApiResponseReader.JsonOptions);

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.RoomType, Is.EqualTo(RoomType.BoardingRoom));
            Assert.That(dto.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(dto.Furnishing, Is.EqualTo(Furnishing.Full));
            Assert.That(dto.AvailableFrom, Is.EqualTo(new DateOnly(2026, 1, 15)));
            Assert.That(dto.HouseRules, Is.Null);
            Assert.That(dto.MonthlyRentVnd, Is.EqualTo(1500000L));
            Assert.That(dto.Utilities.WaterPriceUnit, Is.EqualTo(WaterPriceUnit.PerCubicMeter));
            Assert.That(dto.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(1800000L));
            Assert.That(dto.Amenities, Is.EqualTo(new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.PrivateBathroom }));
            Assert.That(dto.Safety.Features, Is.EqualTo(new[] { SafetyFeature.Cctv, SafetyFeature.SecureEntrance }));
            Assert.That(dto.Photos[0].Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(dto.Address.Ward, Is.EqualTo("Ward 1"));
            Assert.That(dto.Location.Latitude, Is.EqualTo(21.5851));
            Assert.That(dto.IsFavourite, Is.False);
            Assert.That(dto.LastUpdatedAtUtc, Is.EqualTo(new DateTime(2025, 12, 10, 8, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void CampusDto_Deserializes_IsDefault()
    {
        const string defaultJson = """
        { "id": "ICTU", "name": "ICTU - Thai Nguyen University", "shortName": "ICTU", "latitude": 21.5851, "longitude": 105.8066, "isDefault": true }
        """;
        const string otherJson = """
        { "id": "TNUE", "name": "Thai Nguyen University of Education", "shortName": "TNUE", "latitude": 21.5900, "longitude": 105.8100, "isDefault": false }
        """;

        var defaultCampus = JsonSerializer.Deserialize<CampusDto>(defaultJson, ApiResponseReader.JsonOptions);
        var otherCampus = JsonSerializer.Deserialize<CampusDto>(otherJson, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(defaultCampus!.IsDefault, Is.True);
            Assert.That(defaultCampus.Id, Is.EqualTo("ICTU"));
            Assert.That(defaultCampus.ShortName, Is.EqualTo("ICTU"));
            Assert.That(defaultCampus.Latitude, Is.EqualTo(21.5851));
            Assert.That(defaultCampus.Longitude, Is.EqualTo(105.8066));
            Assert.That(otherCampus!.IsDefault, Is.False);
        });
    }

    [Test]
    public void PhotoDto_Deserializes_KindString()
    {
        const string json = """
        { "id": "p1", "kind": "Panorama", "thumbUrl": "https://cdn.example/t.jpg", "mediumUrl": "https://cdn.example/m.jpg", "originalUrl": "https://cdn.example/o.jpg", "width": 1200, "height": 600, "sortOrder": 2 }
        """;

        var dto = JsonSerializer.Deserialize<PhotoDto>(json, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(dto!.Kind, Is.EqualTo(PhotoKind.Panorama));
            Assert.That(dto.Width, Is.EqualTo(1200));
            Assert.That(dto.Height, Is.EqualTo(600));
            Assert.That(dto.SortOrder, Is.EqualTo(2));
            Assert.That(dto.OriginalUrl, Is.EqualTo("https://cdn.example/o.jpg"));
        });
    }

    [Test]
    public void TravelTimesDto_Deserializes_Modes()
    {
        const string json = """
        {
          "campusId": "ICTU",
          "straightLineDistanceKm": 0.3,
          "isFallback": false,
          "items": [
            { "mode": "Motorbike", "distanceMeters": 400, "durationSeconds": 60, "isEstimate": false },
            { "mode": "Bus", "distanceMeters": 500, "durationSeconds": 300, "isEstimate": true },
            { "mode": "Walking", "distanceMeters": 350, "durationSeconds": 280, "isEstimate": false }
          ]
        }
        """;

        var dto = JsonSerializer.Deserialize<TravelTimesDto>(json, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(dto!.Items.Count, Is.EqualTo(3));
            Assert.That(dto.Items[0].Mode, Is.EqualTo(TravelMode.Motorbike));
            Assert.That(dto.Items[1].Mode, Is.EqualTo(TravelMode.Bus));
            Assert.That(dto.Items[1].IsEstimate, Is.True);
            Assert.That(dto.Items[2].Mode, Is.EqualTo(TravelMode.Walking));
            Assert.That(dto.IsFallback, Is.False);
            Assert.That(dto.StraightLineDistanceKm, Is.EqualTo(0.3));
        });
    }

    [Test]
    public void ComparisonDto_Deserializes()
    {
        const string json = """
        {
          "campusId": "ICTU",
          "rooms": [
            { "id": "r1", "title": "Room 1", "coverThumbUrl": null, "status": "Available", "monthlyRentVnd": 1500000, "depositVnd": 1500000, "areaM2": 20, "roomType": "BoardingRoom", "distanceKm": 0.3, "amenities": ["Wifi"], "safetyScore": 4, "isSafetyVerified": true, "estimatedMonthlyTotalVnd": 1800000 },
            { "id": "r2", "title": "Room 2", "coverThumbUrl": "https://cdn.example/t.jpg", "status": "Available", "monthlyRentVnd": 2000000, "depositVnd": 2000000, "areaM2": 25, "roomType": "MiniApartment", "distanceKm": 0.7, "amenities": ["Wifi", "Fridge"], "safetyScore": 3, "isSafetyVerified": false, "estimatedMonthlyTotalVnd": 2300000 }
          ]
        }
        """;

        var dto = JsonSerializer.Deserialize<ComparisonDto>(json, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(dto!.CampusId, Is.EqualTo("ICTU"));
            Assert.That(dto.Rooms.Count, Is.EqualTo(2));
            Assert.That(dto.Rooms[0].RoomType, Is.EqualTo(RoomType.BoardingRoom));
            Assert.That(dto.Rooms[1].RoomType, Is.EqualTo(RoomType.MiniApartment));
            Assert.That(dto.Rooms[0].Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(dto.Rooms[0].CoverThumbUrl, Is.Null);
            Assert.That(dto.Rooms[1].CoverThumbUrl, Is.EqualTo("https://cdn.example/t.jpg"));
            Assert.That(dto.Rooms[1].Amenities, Is.EqualTo(new[] { Amenity.Wifi, Amenity.Fridge }));
            Assert.That(dto.Rooms[0].EstimatedMonthlyTotalVnd, Is.EqualTo(1800000L));
            Assert.That(dto.Rooms[1].IsSafetyVerified, Is.False);
        });
    }

    [Test]
    public void AreaSearchRequest_Serializes_CamelCase_NoServerControlledFields()
    {
        var request = new AreaSearchRequest(
            CampusId: "ICTU",
            MinRentVnd: 1000000,
            MaxRentVnd: 3000000,
            MinAreaM2: 15,
            MaxAreaM2: 40,
            RoomTypes: new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm: 2.5,
            Sort: "distance",
            Polygon: new[]
            {
                new GeoPointDto(21.58, 105.80),
                new GeoPointDto(21.59, 105.81),
                new GeoPointDto(21.57, 105.82)
            });

        var json = JsonSerializer.Serialize(request, ApiResponseReader.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var serverControlled = new[] { "id", "status", "isVerified", "role", "landlordId", "createdAtUtc", "updatedAtUtc", "version" };

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("campusId", out var campusId), Is.True);
            Assert.That(campusId.GetString(), Is.EqualTo("ICTU"));
            Assert.That(root.TryGetProperty("minRentVnd", out _), Is.True);
            Assert.That(root.TryGetProperty("maxDistanceKm", out var maxDistance), Is.True);
            Assert.That(maxDistance.GetDouble(), Is.EqualTo(2.5));
            Assert.That(root.TryGetProperty("sort", out var sort), Is.True);
            Assert.That(sort.GetString(), Is.EqualTo("distance"));
            Assert.That(root.GetProperty("roomTypes").EnumerateArray().Select(e => e.GetString()).ToArray(),
                Is.EqualTo(new[] { "BoardingRoom", "MiniApartment" }));
            Assert.That(root.GetProperty("polygon").GetArrayLength(), Is.EqualTo(3));
            Assert.That(root.GetProperty("polygon")[0].GetProperty("latitude").GetDouble(), Is.EqualTo(21.58));
            Assert.That(root.TryGetProperty("CampusId", out _), Is.False, "Properties must serialize camelCase, not PascalCase.");

            var names = root.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.That(names.Intersect(serverControlled, StringComparer.OrdinalIgnoreCase).ToArray(), Is.Empty,
                "AreaSearchRequest must not carry server-controlled fields (GUARDRAILS 1.5.1).");
        });
    }

    [Test]
    public void PagedNotifications_Deserializes_UnreadCount_AndItems()
    {
        const string json = """
        {
          "items": [
            { "id": "n1", "type": "NewMessage", "title": "New message", "body": "You have a new message", "targetType": "Conversation", "targetId": "c1", "isRead": false, "createdAtUtc": "2026-01-02T03:04:05Z" },
            { "id": "n2", "type": "ListingApproved", "title": "Listing approved", "body": "Your listing is live", "targetType": null, "targetId": null, "isRead": true, "createdAtUtc": "2026-01-01T00:00:00Z" }
          ],
          "totalCount": 2,
          "pageNumber": 1,
          "pageSize": 20,
          "unreadCount": 1
        }
        """;

        var page = JsonSerializer.Deserialize<PagedNotifications>(json, ApiResponseReader.JsonOptions);

        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.UnreadCount, Is.EqualTo(1));
            Assert.That(page.Items.Count, Is.EqualTo(2));
            Assert.That(page.Items[0].Type, Is.EqualTo(NotificationType.NewMessage));
            Assert.That(page.Items[0].TargetType, Is.EqualTo(NotificationTargetType.Conversation));
            Assert.That(page.Items[0].TargetId, Is.EqualTo("c1"));
            Assert.That(page.Items[0].IsRead, Is.False);
            Assert.That(page.Items[1].Type, Is.EqualTo(NotificationType.ListingApproved));
            Assert.That(page.Items[1].TargetType, Is.Null);
            Assert.That(page.Items[1].TargetId, Is.Null);
            Assert.That(page.Items[1].IsRead, Is.True);
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.PageNumber, Is.EqualTo(1));
            Assert.That(page.PageSize, Is.EqualTo(20));
            Assert.That(page.TotalPages, Is.EqualTo(1));
        });
    }

    [Test]
    public void MessagePage_NextBeforeNull()
    {
        const string lastPageJson = """
        {
          "items": [
            { "id": "m1", "conversationId": "c1", "senderId": "u1", "clientMessageId": "11111111-1111-1111-1111-111111111111", "body": "Is the room still available?", "sentAtUtc": "2026-01-02T03:04:05Z" }
          ],
          "nextBefore": null
        }
        """;
        const string olderPageJson = """
        { "items": [], "nextBefore": "2026-01-01T00:00:00Z" }
        """;

        var lastPage = JsonSerializer.Deserialize<MessagePage>(lastPageJson, ApiResponseReader.JsonOptions);
        var olderPage = JsonSerializer.Deserialize<MessagePage>(olderPageJson, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(lastPage!.NextBefore, Is.Null, "null cursor means there are no older messages.");
            Assert.That(lastPage.Items.Count, Is.EqualTo(1));
            Assert.That(lastPage.Items[0].ConversationId, Is.EqualTo("c1"));
            Assert.That(lastPage.Items[0].ClientMessageId, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
            Assert.That(lastPage.Items[0].Body, Is.EqualTo("Is the room still available?"));
            Assert.That(lastPage.Items[0].SentAtUtc, Is.EqualTo(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
            Assert.That(olderPage!.NextBefore, Is.EqualTo("2026-01-01T00:00:00Z"));
            Assert.That(olderPage.Items, Is.Empty);
        });
    }

    [Test]
    public void ConversationDto_Deserializes_RoomAndOtherParty()
    {
        const string json = """
        {
          "id": "c1",
          "room": { "id": "seed-z115", "title": "Student room Z115", "coverThumbUrl": null, "monthlyRentVnd": 1500000, "status": "Available" },
          "otherParty": { "id": "u2", "displayName": "Landlord A", "role": "Landlord" },
          "lastMessagePreview": "Is the room still available?",
          "lastMessageAtUtc": "2026-01-02T03:04:05Z",
          "unreadCount": 2,
          "canSend": true,
          "createdAtUtc": "2026-01-01T00:00:00Z"
        }
        """;
        const string closedJson = """
        {
          "id": "c2",
          "room": { "id": "seed-ktx-32", "title": "Private dorm 3/2", "coverThumbUrl": "https://cdn.example/t.jpg", "monthlyRentVnd": 900000, "status": "Rented" },
          "otherParty": { "id": "u3", "displayName": "Student B", "role": "Student" },
          "lastMessagePreview": null,
          "lastMessageAtUtc": null,
          "unreadCount": 0,
          "canSend": false,
          "createdAtUtc": "2025-12-01T00:00:00Z"
        }
        """;

        var conversation = JsonSerializer.Deserialize<ConversationDto>(json, ApiResponseReader.JsonOptions);
        var closed = JsonSerializer.Deserialize<ConversationDto>(closedJson, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(conversation!.Room.Id, Is.EqualTo("seed-z115"));
            Assert.That(conversation.Room.Title, Is.EqualTo("Student room Z115"));
            Assert.That(conversation.Room.CoverThumbUrl, Is.Null);
            Assert.That(conversation.Room.MonthlyRentVnd, Is.EqualTo(1500000L));
            Assert.That(conversation.Room.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(conversation.OtherParty.Id, Is.EqualTo("u2"));
            Assert.That(conversation.OtherParty.DisplayName, Is.EqualTo("Landlord A"));
            Assert.That(conversation.OtherParty.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(conversation.LastMessagePreview, Is.EqualTo("Is the room still available?"));
            Assert.That(conversation.LastMessageAtUtc, Is.EqualTo(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
            Assert.That(conversation.UnreadCount, Is.EqualTo(2));
            Assert.That(conversation.CanSend, Is.True);
            Assert.That(closed!.Room.Status, Is.EqualTo(ListingStatus.Rented));
            Assert.That(closed.OtherParty.Role, Is.EqualTo(UserRole.Student));
            Assert.That(closed.LastMessagePreview, Is.Null);
            Assert.That(closed.LastMessageAtUtc, Is.Null);
            Assert.That(closed.CanSend, Is.False, "canSend is false 30 days after the listing became unavailable (BR-CHT-07).");
        });
    }

    [Test]
    public void SavedSearchDto_Deserializes_AlertStateString()
    {
        const string json = """
        {
          "id": "ss1",
          "name": "Near ICTU under 2 million",
          "criteria": { "campusId": "ICTU", "maxRentVnd": 2000000, "roomTypes": ["BoardingRoom"], "maxDistanceKm": 2.0, "sort": "distance" },
          "alertState": "Active",
          "createdAtUtc": "2026-01-01T00:00:00Z",
          "updatedAtUtc": "2026-01-02T00:00:00Z"
        }
        """;

        var dto = JsonSerializer.Deserialize<SavedSearchDto>(json, ApiResponseReader.JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(dto!.AlertState, Is.EqualTo(AlertState.Active));
            Assert.That(dto.Id, Is.EqualTo("ss1"));
            Assert.That(dto.Name, Is.EqualTo("Near ICTU under 2 million"));
            Assert.That(dto.Criteria.CampusId, Is.EqualTo("ICTU"));
            Assert.That(dto.Criteria.MaxRentVnd, Is.EqualTo(2000000L));
            Assert.That(dto.Criteria.MinRentVnd, Is.Null);
            Assert.That(dto.Criteria.RoomTypes, Is.EqualTo(new[] { RoomType.BoardingRoom }));
            Assert.That(dto.Criteria.MaxDistanceKm, Is.EqualTo(2.0));
            Assert.That(dto.Criteria.Sort, Is.EqualTo("distance"));
            Assert.That(dto.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(dto.UpdatedAtUtc, Is.EqualTo(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void UpdateMyProfileRequest_Serializes_NoRoleOrStatusFields()
    {
        var request = new UpdateMyProfileRequest(
            FullName: "Nguyen Van A",
            PhoneNumber: "0912345678",
            SelectedCampusId: "ICTU",
            PreferredLanguage: "vi");

        var json = JsonSerializer.Serialize(request, ApiResponseReader.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var serverControlled = new[] { "id", "email", "role", "status", "isVerified", "authProvider", "landlordId", "createdAtUtc", "updatedAtUtc", "version" };

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("fullName", out var fullName), Is.True);
            Assert.That(fullName.GetString(), Is.EqualTo("Nguyen Van A"));
            Assert.That(root.TryGetProperty("phoneNumber", out var phone), Is.True);
            Assert.That(phone.GetString(), Is.EqualTo("0912345678"));
            Assert.That(root.TryGetProperty("selectedCampusId", out var campusId), Is.True);
            Assert.That(campusId.GetString(), Is.EqualTo("ICTU"));
            Assert.That(root.TryGetProperty("preferredLanguage", out var language), Is.True);
            Assert.That(language.GetString(), Is.EqualTo("vi"));
            Assert.That(root.TryGetProperty("FullName", out _), Is.False, "Properties must serialize camelCase, not PascalCase.");

            var names = root.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.That(names.Intersect(serverControlled, StringComparer.OrdinalIgnoreCase).ToArray(), Is.Empty,
                "UpdateMyProfileRequest must not carry server-controlled fields (GUARDRAILS 1.5.1).");
        });
    }

    [Test]
    public void RegisterDeviceRequest_Serializes_PlatformString()
    {
        var android = new RegisterDeviceRequest(PushToken: "fcm-token-1", Platform: DevicePlatform.Android);
        var ios = new RegisterDeviceRequest(PushToken: "apns-token-1", Platform: DevicePlatform.iOS);

        var androidJson = JsonSerializer.Serialize(android, ApiResponseReader.JsonOptions);
        var iosJson = JsonSerializer.Serialize(ios, ApiResponseReader.JsonOptions);
        using var androidDocument = JsonDocument.Parse(androidJson);
        using var iosDocument = JsonDocument.Parse(iosJson);

        Assert.Multiple(() =>
        {
            Assert.That(androidDocument.RootElement.TryGetProperty("pushToken", out var token), Is.True);
            Assert.That(token.GetString(), Is.EqualTo("fcm-token-1"));
            Assert.That(androidDocument.RootElement.TryGetProperty("platform", out var platform), Is.True);
            Assert.That(platform.GetString(), Is.EqualTo("Android"));
            Assert.That(iosDocument.RootElement.GetProperty("platform").GetString(), Is.EqualTo("iOS"));
        });
    }
}