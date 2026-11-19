using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>
/// Unit tests for T00.9 Phase 2: Room aggregate extensions for amenities, safety features, and versioned house rules.
/// Verifies BR-LST-08, BR-HR-01..02 and aggregate invariant guards.
/// </summary>
[TestFixture]
public class RoomChildrenDomainTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Room CreateTestRoom(DateTime? utcNow = null) => Room.Create(
        landlordId: "landlord-test",
        title: "Phòng trọ cao cấp gần ICTU",
        roomType: RoomType.BoardingRoom,
        monthlyRentVnd: 2_000_000L,
        depositVnd: 2_000_000L,
        areaM2: 25.0,
        addressLine: "123 Đường Z115",
        ward: "Tân Thịnh",
        district: "TP. Thái Nguyên",
        latitude: 21.5851,
        longitude: 105.8066,
        description: "Phòng mới xây khép kín",
        utcNow: utcNow ?? FixedNow);

    // ----------------------------------------------------------------- Amenities Tests

    [Test]
    public void Room_SetAmenities_StoresDistinctAmenitiesAndTouchesAggregate()
    {
        var room = CreateTestRoom();
        var tokenBefore = room.ConcurrencyToken;

        room.SetAmenities(new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.Wifi, Amenity.WaterHeater });

        Assert.Multiple(() =>
        {
            Assert.That(room.Amenities, Has.Count.EqualTo(3));
            Assert.That(room.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.WaterHeater }));
            Assert.That(room.ConcurrencyToken, Is.Not.EqualTo(tokenBefore));
            Assert.That(room.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public void Room_SetAmenities_WithInvalidEnumValue_ThrowsDomainValidationException()
    {
        var room = CreateTestRoom();

        var ex = Assert.Throws<DomainValidationException>(() =>
            room.SetAmenities(new[] { Amenity.Wifi, (Amenity)999 }));

        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public void Room_Create_WithInitialAmenities_InitializesCorrectly()
    {
        var room = Room.Create(
            landlordId: "landlord-test",
            title: "Phòng trọ có sẵn tiện nghi",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 20.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            amenities: new[] { Amenity.Bed, Amenity.Wardrobe, Amenity.Fridge });

        Assert.That(room.Amenities, Is.EquivalentTo(new[] { Amenity.Bed, Amenity.Wardrobe, Amenity.Fridge }));
    }

    // ----------------------------------------------------------------- Safety Features Tests (BR-LST-08)

    [Test]
    public void Room_DeclareSafetyFeatures_StartsUnconfirmedAndCalculatesUnverifiedScore()
    {
        var room = CreateTestRoom();

        room.DeclareSafetyFeatures(new[] { SafetyFeature.FireSafety, SafetyFeature.Cctv, SafetyFeature.FireSafety });

        Assert.Multiple(() =>
        {
            Assert.That(room.SafetyFeatures, Has.Count.EqualTo(2));
            Assert.That(room.SafetyFeatures.All(f => !f.IsConfirmed), Is.True);
            Assert.That(room.IsSafetyVerified, Is.False);
            // When not verified, SafetyScore = count of declared features (BR-LST-08)
            Assert.That(room.SafetyScore, Is.EqualTo(2));
        });
    }

    [Test]
    public void Room_ConfirmSafetyFeatures_MarksConfirmedAndCalculatesVerifiedScore()
    {
        var room = CreateTestRoom();
        room.DeclareSafetyFeatures(new[] { SafetyFeature.FireSafety, SafetyFeature.Cctv, SafetyFeature.SecureEntrance });

        // Admin confirms only FireSafety and SecureEntrance
        room.ConfirmSafetyFeatures(new[] { SafetyFeature.FireSafety, SafetyFeature.SecureEntrance }, "admin-42", FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(room.IsSafetyVerified, Is.True);
            Assert.That(room.VerifiedByUserId, Is.EqualTo("admin-42"));
            Assert.That(room.VerifiedAtUtc, Is.EqualTo(FixedNow));
            // When verified, SafetyScore = count of confirmed features only (BR-LST-08)
            Assert.That(room.SafetyScore, Is.EqualTo(2));

            var confirmedFeatures = room.SafetyFeatures.Where(f => f.IsConfirmed).Select(f => f.Feature);
            Assert.That(confirmedFeatures, Is.EquivalentTo(new[] { SafetyFeature.FireSafety, SafetyFeature.SecureEntrance }));

            var unconfirmedFeatures = room.SafetyFeatures.Where(f => !f.IsConfirmed).Select(f => f.Feature);
            Assert.That(unconfirmedFeatures, Is.EquivalentTo(new[] { SafetyFeature.Cctv }));
        });
    }

    [Test]
    public void Room_ConfirmSafetyFeatures_WhenReconfirmedWithFewerFeatures_RevokesUnconfirmedFeaturesAndDecreasesSafetyScore()
    {
        var room = CreateTestRoom();
        room.DeclareSafetyFeatures(new[] { SafetyFeature.FireSafety, SafetyFeature.Cctv, SafetyFeature.SecureEntrance });

        // Admin initial confirmation: confirms FireSafety and SecureEntrance
        room.ConfirmSafetyFeatures(new[] { SafetyFeature.FireSafety, SafetyFeature.SecureEntrance }, "admin-1", FixedNow);
        Assert.That(room.SafetyScore, Is.EqualTo(2));

        // Admin re-reviews/re-moderates: revokes SecureEntrance, only confirms FireSafety (Idempotent test)
        room.ConfirmSafetyFeatures(new[] { SafetyFeature.FireSafety }, "admin-2", FixedNow.AddDays(1));

        Assert.Multiple(() =>
        {
            Assert.That(room.IsSafetyVerified, Is.True);
            Assert.That(room.SafetyScore, Is.EqualTo(1), "Score decreases to 1 when SecureEntrance is revoked");
            var confirmed = room.SafetyFeatures.Where(f => f.IsConfirmed).Select(f => f.Feature);
            Assert.That(confirmed, Is.EquivalentTo(new[] { SafetyFeature.FireSafety }));
            var unconfirmed = room.SafetyFeatures.Where(f => !f.IsConfirmed).Select(f => f.Feature);
            Assert.That(unconfirmed, Is.EquivalentTo(new[] { SafetyFeature.Cctv, SafetyFeature.SecureEntrance }));
            Assert.That(room.VerifiedByUserId, Is.EqualTo("admin-2"));
        });
    }

    [Test]
    public void Room_DeclareSafetyFeatures_WhenListingAlreadyAvailable_FlagsNeedsReReview()
    {
        var room = CreateTestRoom();
        room.Approve(FixedNow);
        Assert.That(room.NeedsReReview, Is.False);

        // Landlord adds a new safety feature to an active listing (docs/erd.md §4.6)
        room.DeclareSafetyFeatures(new[] { SafetyFeature.FireSafety });

        Assert.That(room.NeedsReReview, Is.True, "Adding safety features to an active listing flags NeedsReReview = 1");
    }

    // ----------------------------------------------------------------- House Rules Tests (BR-HR-01..02)

    [Test]
    public void Room_AddHouseRuleVersion_SequentiallyIncrementsVersionAndPointsCurrentToLatest()
    {
        var room = CreateTestRoom();
        Assert.That(room.CurrentHouseRules, Is.Null);

        // Add version 1
        room.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.Allowed,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-1",
            quietHoursStart: "22:00",
            quietHoursEnd: "06:00",
            utcNow: FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(room.HouseRuleHistory, Has.Count.EqualTo(1));
            Assert.That(room.CurrentHouseRules, Is.Not.Null);
            Assert.That(room.CurrentHouseRules!.VersionNumber, Is.EqualTo(1));
            Assert.That(room.CurrentHouseRules.QuietHoursStart, Is.EqualTo("22:00"));
            Assert.That(room.CurrentHouseRules.QuietHoursEnd, Is.EqualTo("06:00"));
        });

        // Add version 2 (updated rule)
        room.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.SharedKitchenOnly,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-1",
            quietHoursStart: "23:00",
            quietHoursEnd: "05:00",
            utcNow: FixedNow.AddDays(5));

        Assert.Multiple(() =>
        {
            Assert.That(room.HouseRuleHistory, Has.Count.EqualTo(2));
            Assert.That(room.CurrentHouseRules!.VersionNumber, Is.EqualTo(2), "CurrentHouseRules points to latest version number");
            Assert.That(room.CurrentHouseRules.GuestPolicy, Is.EqualTo(GuestPolicy.DaytimeOnly));
            Assert.That(room.CurrentHouseRules.CookingPolicy, Is.EqualTo(CookingPolicy.SharedKitchenOnly));
            Assert.That(room.CurrentHouseRules.QuietHoursStart, Is.EqualTo("23:00"));
        });
    }

    [Test]
    public void Room_AddHouseRuleVersion_EnforcesInvariantFieldLimits()
    {
        var room = CreateTestRoom();

        // 1. Invalid time format
        var timeEx = Assert.Throws<DomainValidationException>(() =>
            room.AddHouseRuleVersion(
                guestPolicy: GuestPolicy.Allowed,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: "landlord-1",
                quietHoursStart: "25:00")); // Invalid hour
        Assert.That(timeEx!.ErrorCode, Is.EqualTo("INVALID_TIME_FORMAT"));

        // 2. Parking rules exceeds 300 chars (BR-HR-01)
        var longParking = new string('A', 301);
        var parkingEx = Assert.Throws<DomainValidationException>(() =>
            room.AddHouseRuleVersion(
                guestPolicy: GuestPolicy.Allowed,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: "landlord-1",
                parkingRules: longParking));
        Assert.That(parkingEx!.ErrorCode, Is.EqualTo("PARKING_RULES_TOO_LONG"));

        // 3. Other rules exceeds 1000 chars (BR-HR-01)
        var longOther = new string('B', 1001);
        var otherEx = Assert.Throws<DomainValidationException>(() =>
            room.AddHouseRuleVersion(
                guestPolicy: GuestPolicy.Allowed,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: "landlord-1",
                otherRules: longOther));
        Assert.That(otherEx!.ErrorCode, Is.EqualTo("OTHER_RULES_TOO_LONG"));

        // 4. Guest policy note exceeds 200 chars (Low finding 1 / openapi.yaml#L1489)
        var longNote = new string('C', 201);
        var noteEx = Assert.Throws<DomainValidationException>(() =>
            room.AddHouseRuleVersion(
                guestPolicy: GuestPolicy.Allowed,
                cookingPolicy: CookingPolicy.Allowed,
                parkingAvailable: true,
                petsAllowed: false,
                smokingAllowed: false,
                createdByUserId: "landlord-1",
                guestPolicyNote: longNote));
        Assert.That(noteEx!.ErrorCode, Is.EqualTo("GUEST_POLICY_NOTE_TOO_LONG"));
    }

    [Test]
    public void Room_AddHouseRuleVersion_NonSequentialVersion_ThrowsDomainValidationException()
    {
        var room = CreateTestRoom();

        // Trying to directly attach version 3 when room has no history
        var skippedVersion = new HouseRuleVersion(
            id: Guid.NewGuid().ToString("N"),
            roomId: room.Id,
            versionNumber: 3,
            guestPolicy: GuestPolicy.Allowed,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-1",
            createdAtUtc: FixedNow);

        var ex = Assert.Throws<DomainValidationException>(() =>
            room.AddHouseRuleVersion(skippedVersion));

        Assert.That(ex!.ErrorCode, Is.EqualTo("INVALID_VERSION_NUMBER"));
    }

    [Test]
    public void Room_AddHouseRuleVersion_MismatchedRoomId_ThrowsDomainValidationException()
    {
        var room = CreateTestRoom();

        var foreignRule = new HouseRuleVersion(
            id: Guid.NewGuid().ToString("N"),
            roomId: "other-room-id",
            versionNumber: 1,
            guestPolicy: GuestPolicy.Allowed,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-1",
            createdAtUtc: FixedNow);

        var ex = Assert.Throws<DomainValidationException>(() =>
            room.AddHouseRuleVersion(foreignRule));

        Assert.That(ex!.Message, Does.Contain("belongs to room"));
    }

    // ----------------------------------------------------------------- Reconstitute Tests

    [Test]
    public void Room_Reconstitute_WithAllChildren_RebuildsCorrectAggregateState()
    {
        var roomId = Guid.NewGuid().ToString("N");
        var amenities = new[] { Amenity.AirConditioner, Amenity.Wifi };
        var safety = new[] { new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, IsConfirmed: true) };
        var history = new[]
        {
            new HouseRuleVersion("hr-1", roomId, 1, GuestPolicy.Allowed, CookingPolicy.Allowed, true, false, false, "u1", FixedNow)
        };

        var room = Room.Reconstitute(
            id: roomId,
            landlordId: "u1",
            title: "Phòng trọ tái tạo từ DB",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_500_000L,
            depositVnd: 2_500_000L,
            areaM2: 25.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            status: ListingStatus.Available,
            createdAtUtc: FixedNow,
            lastUpdatedAtUtc: FixedNow,
            version: 5,
            isSafetyVerified: true,
            amenities: amenities,
            safetyFeatures: safety,
            houseRuleHistory: history);

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.EqualTo(roomId));
            Assert.That(room.Amenities, Is.EquivalentTo(amenities));
            Assert.That(room.SafetyFeatures, Has.Count.EqualTo(1));
            Assert.That(room.SafetyFeatures.First().Feature, Is.EqualTo(SafetyFeature.FireSafety));
            Assert.That(room.SafetyFeatures.First().IsConfirmed, Is.True);
            Assert.That(room.SafetyScore, Is.EqualTo(1));
            Assert.That(room.HouseRuleHistory, Has.Count.EqualTo(1));
            Assert.That(room.CurrentHouseRules!.VersionNumber, Is.EqualTo(1));
        });
    }
}
