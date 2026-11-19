using System.Reflection;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>
/// US08 / BR-LST-01..09 / BR-HR-01..02 / ADR-011: listing invariants, child collections and the
/// edit rules of <c>Room.UpdateDetails</c> (material changes vs re-review).
/// </summary>
[TestFixture]
public class RoomListingLifecycleAndEditTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

    private static Room CreateValidRoom(DateTime? utcNow = null) => Room.Create(
        landlordId: "landlord-1",
        title: "Phòng trọ Sinh viên Z115",
        roomType: RoomType.BoardingRoom,
        monthlyRentVnd: 1_500_000L,
        depositVnd: 1_500_000L,
        areaM2: 18.0,
        addressLine: "Z115",
        ward: "Tân Thịnh",
        district: "TP. Thái Nguyên",
        latitude: 21.5875,
        longitude: 105.8080,
        description: "Phòng sạch sẽ, gần trường ICTU",
        utcNow: utcNow ?? FixedNow);

    private static Room CreateRoomInStatus(ListingStatus status)
    {
        var room = CreateValidRoom();
        switch (status)
        {
            case ListingStatus.PendingReview:
                return room;
            case ListingStatus.Available:
                room.Approve(FixedNow);
                return room;
            case ListingStatus.Rejected:
                room.Reject("Thiếu ảnh thực tế");
                return room;
            case ListingStatus.Rented:
                room.Approve(FixedNow);
                room.MarkRented();
                return room;
            case ListingStatus.Unpublished:
                room.Approve(FixedNow);
                room.Unpublish();
                return room;
            case ListingStatus.Suspended:
                room.Approve(FixedNow);
                room.Suspend("Bị báo cáo lừa đảo");
                return room;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    /// <summary>Non-material snapshot of the room that UpdateDetails treats as "no change".</summary>
    private static RoomDetails DetailsFrom(Room room) => new(
        room.Title,
        room.Description,
        room.RoomType,
        room.MonthlyRentVnd,
        room.DepositVnd,
        room.AreaM2,
        room.MaxOccupants,
        room.Furnishing,
        room.AvailableFrom,
        room.AddressLine,
        room.Ward,
        room.District,
        room.Latitude,
        room.Longitude,
        room.ElectricityPriceVndPerKwh,
        room.WaterPriceVnd,
        room.WaterPriceUnit,
        room.InternetFeeVnd,
        room.ParkingFeeVnd,
        room.OtherFeesNote,
        room.Amenities,
        room.SafetyFeatures.Select(entry => entry.Feature).ToArray(),
        HasAddedNewPhotos: false);

    // ----------------------------------------------------------------- BR-LST-01 bounding box

    [TestCase(21.29, 105.80, TestName = "Create_LatitudeBelowThaiNguyenBox_Throws")]
    [TestCase(22.06, 105.80, TestName = "Create_LatitudeAboveThaiNguyenBox_Throws")]
    [TestCase(21.50, 105.44, TestName = "Create_LongitudeBelowThaiNguyenBox_Throws")]
    [TestCase(21.50, 106.26, TestName = "Create_LongitudeAboveThaiNguyenBox_Throws")]
    [TestCase(double.NaN, 105.80, TestName = "Create_LatitudeNaN_Throws")]
    [TestCase(21.50, double.PositiveInfinity, TestName = "Create_LongitudeInfinity_Throws")]
    public void Create_CoordinatesOutsideThaiNguyenBoundingBox_Throws(double latitude, double longitude)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", latitude, longitude));
    }

    [TestCase(21.30, 105.45, TestName = "Create_CoordinatesOnSouthWestCorner_IsAccepted")]
    [TestCase(22.05, 106.25, TestName = "Create_CoordinatesOnNorthEastCorner_IsAccepted")]
    public void Create_CoordinatesOnThaiNguyenBoundingBox_IsAccepted(double latitude, double longitude)
    {
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", latitude, longitude);

        Assert.Multiple(() =>
        {
            Assert.That(room.Latitude, Is.EqualTo(latitude));
            Assert.That(room.Longitude, Is.EqualTo(longitude));
        });
    }

    // ----------------------------------------------------------------- BR-LST-01/02/03 length limits

    [Test]
    public void Create_DescriptionLongerThan2000Chars_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            description: new string('a', Room.DescriptionMaxLength + 1)));
    }

    [Test]
    public void Create_DescriptionExactly2000Chars_IsAccepted()
    {
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            description: new string('a', Room.DescriptionMaxLength));

        Assert.That(room.Description, Has.Length.EqualTo(2000));
    }

    [Test]
    public void Create_AddressLineLongerThan200Chars_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            new string('a', Room.AddressLineMaxLength + 1), "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_WardLongerThan100Chars_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", new string('a', Room.WardMaxLength + 1), "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_DistrictLongerThan100Chars_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", new string('a', Room.DistrictMaxLength + 1), 21.5875, 105.8080));
    }

    [Test]
    public void Create_OtherFeesNoteLongerThan200Chars_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            otherFeesNote: new string('a', Room.OtherFeesNoteMaxLength + 1)));
    }

    // ----------------------------------------------------------------- BR-LST-03 fees

    [Test]
    public void Create_WithNegativeWaterOrInternetOrParkingFee_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
                waterPriceVnd: -1), "WaterPriceVnd");
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
                internetFeeVnd: -1), "InternetFeeVnd");
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
                parkingFeeVnd: -1), "ParkingFeeVnd");
        });
    }

    [Test]
    public void Create_WithZeroFees_IsAccepted()
    {
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            waterPriceVnd: 0,
            internetFeeVnd: 0,
            parkingFeeVnd: 0);

        Assert.Multiple(() =>
        {
            Assert.That(room.WaterPriceVnd, Is.Zero);
            Assert.That(room.InternetFeeVnd, Is.Zero);
            Assert.That(room.ParkingFeeVnd, Is.Zero);
        });
    }

    // ----------------------------------------------------------------- GUARDRAILS 1.5.1 whitelists

    [TestCase("Luxury", TestName = "Create_UnknownFurnishing_Throws")]
    [TestCase("full", TestName = "Create_LowercaseFurnishing_Throws")]
    [TestCase("1", TestName = "Create_NumericFurnishing_Throws")]
    [TestCase("Unfurnished, Basic", TestName = "Create_CombinedFurnishingNames_Throws")]
    public void Create_WithFurnishingOutsideWhitelist_Throws(string furnishing)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            furnishing: furnishing));
    }

    [TestCase("Full", TestName = "Create_FurnishingFull_IsAccepted")]
    [TestCase("Basic", TestName = "Create_FurnishingBasic_IsAccepted")]
    [TestCase("Unfurnished", TestName = "Create_FurnishingUnfurnished_IsAccepted")]
    public void Create_WithWhitelistedFurnishing_IsAccepted(string furnishing)
    {
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            furnishing: furnishing);

        Assert.That(room.Furnishing, Is.EqualTo(furnishing));
    }

    [TestCase("PerM3", TestName = "Create_UnknownWaterPriceUnit_Throws")]
    [TestCase("perperson", TestName = "Create_LowercaseWaterPriceUnit_Throws")]
    [TestCase("2", TestName = "Create_NumericWaterPriceUnit_Throws")]
    [TestCase("PerPerson, Included", TestName = "Create_CombinedWaterPriceUnitNames_Throws")]
    public void Create_WithWaterPriceUnitOutsideWhitelist_Throws(string waterPriceUnit)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            waterPriceUnit: waterPriceUnit));
    }

    [Test]
    public void Create_WithWhitelistedWaterPriceUnit_IsAccepted()
    {
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            waterPriceUnit: "Included");

        Assert.That(room.WaterPriceUnit, Is.EqualTo("Included"));
    }

    // ----------------------------------------------------------------- SetChildren

    [Test]
    public void SetChildren_DuplicateAmenities_KeepsFirstOccurrenceAndStableOrder()
    {
        var room = CreateValidRoom();

        room.SetChildren(
            new[] { Amenity.Wifi, Amenity.Bed, Amenity.Wifi, Amenity.Desk, Amenity.Bed },
            safetyFeatures: null);

        Assert.Multiple(() =>
        {
            Assert.That(room.Amenities, Is.EqualTo(new[] { Amenity.Wifi, Amenity.Bed, Amenity.Desk }));
            Assert.That(room.Version, Is.EqualTo(1), "SetChildren is part of creation and must not Touch().");
            Assert.That(room.SafetyFeatures, Is.Empty);
            Assert.That(room.CurrentHouseRules, Is.Null);
        });
    }

    [Test]
    public void SetChildren_WithUndefinedAmenity_Throws()
    {
        var room = CreateValidRoom();

        Assert.Throws<DomainValidationException>(() => room.SetChildren(new[] { (Amenity)99 }, null));
    }

    [Test]
    public void SetChildren_DuplicateSafetyFeatures_KeepsFirstEntryAndItsConfirmation()
    {
        var room = CreateValidRoom();
        var first = new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true);

        room.SetChildren(null, new[] { first, new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: false) });

        Assert.That(room.SafetyFeatures, Is.EqualTo(new[] { first }));
    }

    [Test]
    public void SetChildren_WithUndefinedSafetyFeature_Throws()
    {
        var room = CreateValidRoom();

        Assert.Throws<DomainValidationException>(() =>
            room.SetChildren(null, new[] { new RoomSafetyFeatureEntry((SafetyFeature)99, false) }));
    }

    [Test]
    public void SetChildren_WithHouseRules_SetsCurrentHouseRules()
    {
        var room = CreateValidRoom();
        var rules = HouseRuleVersion.Create(
            room.Id, 1, GuestPolicy.Allowed, CookingPolicy.Allowed, true, false, false, "landlord-1", FixedNow);

        room.SetChildren(null, null, rules);

        Assert.That(room.CurrentHouseRules, Is.SameAs(rules));
    }

    // ----------------------------------------------------------------- Reconstitute with children

    [Test]
    public void Reconstitute_WithChildren_LoadsAmenitiesSafetyAndHouseRules()
    {
        var rules = HouseRuleVersion.Create(
            "room-1", 2, GuestPolicy.DaytimeOnly, CookingPolicy.SharedKitchenOnly, false, true, false, "landlord-1", FixedNow);

        var room = Room.Reconstitute(
            "room-1", "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom,
            1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            ListingStatus.Available, FixedNow, FixedNow, version: 3,
            amenities: new[] { Amenity.Wifi, Amenity.Bed },
            safetyFeatures: new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, true) },
            currentHouseRules: rules);

        Assert.Multiple(() =>
        {
            Assert.That(room.Amenities, Is.EqualTo(new[] { Amenity.Wifi, Amenity.Bed }));
            Assert.That(room.SafetyFeatures, Is.EqualTo(new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, true) }));
            Assert.That(room.CurrentHouseRules, Is.SameAs(rules));
        });
    }

    [Test]
    public void Reconstitute_WithUndefinedAmenity_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Reconstitute(
            "room-1", "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom,
            1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            ListingStatus.Available, FixedNow, FixedNow, version: 3,
            amenities: new[] { (Amenity)99 }));
    }

    // ----------------------------------------------------------------- UpdateDetails: no-change edits

    [Test]
    public void UpdateDetails_NonMaterialChange_OnAvailable_KeepsStatusAndDoesNotNeedReReview()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);
        var versionBefore = room.Version;
        var lastUpdatedBefore = room.LastUpdatedAtUtc;

        room.UpdateDetails(DetailsFrom(room) with
        {
            MonthlyRentVnd = 1_600_000L,
            Description = "Mô tả mới, vẫn gần ICTU",
            Amenities = new[] { Amenity.Wifi, Amenity.Bed }
        });

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(room.NeedsReReview, Is.False);
            Assert.That(room.MonthlyRentVnd, Is.EqualTo(1_600_000L));
            Assert.That(room.Amenities, Is.EqualTo(new[] { Amenity.Wifi, Amenity.Bed }));
            Assert.That(room.Version, Is.EqualTo(versionBefore + 1)); // BR-LST-09
            Assert.That(room.LastUpdatedAtUtc, Is.GreaterThanOrEqualTo(lastUpdatedBefore));
        });
    }

    [Test]
    public void UpdateDetails_OnPendingReview_KeepsStatusAndClearsNeedsReReview()
    {
        var room = Room.Reconstitute(
            "room-1", "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom,
            1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            ListingStatus.PendingReview, FixedNow, FixedNow, version: 1, needsReReview: true);

        room.UpdateDetails(DetailsFrom(room) with { AddressLine = "Ngõ 12" });

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(room.NeedsReReview, Is.False);
        });
    }

    // ----------------------------------------------------------------- UpdateDetails: material changes

    [TestCase(ListingStatus.Available, TestName = "UpdateDetails_AddressChangeOnAvailable_SetsNeedsReReview")]
    [TestCase(ListingStatus.Rented, TestName = "UpdateDetails_AddressChangeOnRented_SetsNeedsReReview")]
    [TestCase(ListingStatus.Unpublished, TestName = "UpdateDetails_AddressChangeOnUnpublished_SetsNeedsReReview")]
    public void UpdateDetails_MaterialAddressChange_OnLiveStatus_SetsNeedsReReview(ListingStatus status)
    {
        var room = CreateRoomInStatus(status);

        room.UpdateDetails(DetailsFrom(room) with { AddressLine = "Ngõ 12, tổ 5" });

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(status)); // BR-LST-05: listing stays visible
            Assert.That(room.NeedsReReview, Is.True);
        });
    }

    [Test]
    public void UpdateDetails_PinChange_SetsNeedsReReview()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.UpdateDetails(DetailsFrom(room) with { Latitude = 21.5900, Longitude = 105.8100 });

        Assert.That(room.NeedsReReview, Is.True);
    }

    [Test]
    public void UpdateDetails_RoomTypeChange_SetsNeedsReReview()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.UpdateDetails(DetailsFrom(room) with { RoomType = RoomType.MiniApartment });

        Assert.Multiple(() =>
        {
            Assert.That(room.RoomType, Is.EqualTo(RoomType.MiniApartment));
            Assert.That(room.NeedsReReview, Is.True);
        });
    }

    [Test]
    public void UpdateDetails_AddingNewPhotos_SetsNeedsReReview()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.UpdateDetails(DetailsFrom(room) with { HasAddedNewPhotos = true });

        Assert.That(room.NeedsReReview, Is.True);
    }

    [Test]
    public void UpdateDetails_ReorderingOrRemovingPhotos_DoesNotSetNeedsReReview()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.UpdateDetails(DetailsFrom(room) with { HasAddedNewPhotos = false });

        Assert.That(room.NeedsReReview, Is.False);
    }

    [Test]
    public void UpdateDetails_AddingSafetyFeature_SetsNeedsReReviewAndKeepsExistingConfirmation()
    {
        var room = CreateValidRoom();
        room.SetChildren(null, new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true) });
        room.Approve(FixedNow);

        room.UpdateDetails(DetailsFrom(room) with
        {
            SafetyFeatures = new[] { SafetyFeature.Cctv, SafetyFeature.FireSafety }
        });

        Assert.Multiple(() =>
        {
            Assert.That(room.NeedsReReview, Is.True);
            Assert.That(room.SafetyFeatures, Is.EqualTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true),
                new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, IsConfirmed: false)
            }));
        });
    }

    [Test]
    public void UpdateDetails_KeepingExistingSafetyFeatures_IsNotMaterialAndKeepsConfirmation()
    {
        var room = CreateValidRoom();
        room.SetChildren(null, new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true) });
        room.Approve(FixedNow);

        room.UpdateDetails(DetailsFrom(room));

        Assert.Multiple(() =>
        {
            Assert.That(room.NeedsReReview, Is.False);
            Assert.That(room.SafetyFeatures, Is.EqualTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true)
            }));
        });
    }

    [Test]
    public void UpdateDetails_DuplicateAmenitiesAndSafetyFeatures_AreDeduplicated()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.UpdateDetails(DetailsFrom(room) with
        {
            Amenities = new[] { Amenity.Wifi, Amenity.Bed, Amenity.Wifi },
            SafetyFeatures = new[] { SafetyFeature.Cctv, SafetyFeature.Cctv }
        });

        Assert.Multiple(() =>
        {
            Assert.That(room.Amenities, Is.EqualTo(new[] { Amenity.Wifi, Amenity.Bed }));
            Assert.That(room.SafetyFeatures, Is.EqualTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: false)
            }));
            Assert.That(room.NeedsReReview, Is.True, "A brand-new safety feature is material.");
        });
    }

    // ----------------------------------------------------------------- UpdateDetails: status branches

    [Test]
    public void UpdateDetails_OnRejected_ReturnsToPendingReviewAndClearsReason()
    {
        var room = CreateRoomInStatus(ListingStatus.Rejected);

        room.UpdateDetails(DetailsFrom(room) with { MonthlyRentVnd = 1_400_000L });

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(room.StatusReason, Is.Null);
            Assert.That(room.NeedsReReview, Is.False);
        });
    }

    [Test]
    public void UpdateDetails_OnSuspended_LeavesStatusAndNeedsReReviewUnchanged()
    {
        var room = CreateRoomInStatus(ListingStatus.Suspended);

        room.UpdateDetails(DetailsFrom(room) with { AddressLine = "Ngõ 12, tổ 5" });

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Suspended));
            Assert.That(room.StatusReason, Is.EqualTo("Bị báo cáo lừa đảo"));
            Assert.That(room.NeedsReReview, Is.False);
        });
    }

    [Test]
    public void UpdateDetails_WithInvalidData_ThrowsAndDoesNotBumpVersion()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);
        var versionBefore = room.Version;

        Assert.Throws<DomainValidationException>(() =>
            room.UpdateDetails(DetailsFrom(room) with { WaterPriceVnd = -1 }));

        Assert.That(room.Version, Is.EqualTo(versionBefore));
    }

    [Test]
    public void UpdateDetails_WithNullDetails_ThrowsArgumentNullException()
    {
        var room = CreateValidRoom();

        Assert.Throws<ArgumentNullException>(() => room.UpdateDetails(null!));
    }

    // ----------------------------------------------------------------- HouseRuleVersion (BR-HR-01)

    [Test]
    public void HouseRuleVersion_Create_WithValidFields_StoresValuesAndUtc()
    {
        var rules = HouseRuleVersion.Create(
            roomId: "room-1",
            versionNumber: 1,
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.SharedKitchenOnly,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-1",
            utcNow: FixedNow,
            quietHoursStart: "22:00",
            quietHoursEnd: "06:30",
            curfewTime: "23:00",
            guestPolicyNote: "  Chỉ khách ban ngày  ",
            parkingRules: "Xe máy xếp gọn",
            otherRules: "Không hút thuốc trong phòng");

        Assert.Multiple(() =>
        {
            Assert.That(rules.Id, Has.Length.EqualTo(32));
            Assert.That(rules.RoomId, Is.EqualTo("room-1"));
            Assert.That(rules.VersionNumber, Is.EqualTo(1));
            Assert.That(rules.QuietHoursStart, Is.EqualTo("22:00"));
            Assert.That(rules.QuietHoursEnd, Is.EqualTo("06:30"));
            Assert.That(rules.CurfewTime, Is.EqualTo("23:00"));
            Assert.That(rules.GuestPolicy, Is.EqualTo(GuestPolicy.DaytimeOnly));
            Assert.That(rules.GuestPolicyNote, Is.EqualTo("Chỉ khách ban ngày"));
            Assert.That(rules.CookingPolicy, Is.EqualTo(CookingPolicy.SharedKitchenOnly));
            Assert.That(rules.ParkingAvailable, Is.True);
            Assert.That(rules.ParkingRules, Is.EqualTo("Xe máy xếp gọn"));
            Assert.That(rules.PetsAllowed, Is.False);
            Assert.That(rules.SmokingAllowed, Is.False);
            Assert.That(rules.OtherRules, Is.EqualTo("Không hút thuốc trong phòng"));
            Assert.That(rules.CreatedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(rules.CreatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(rules.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [TestCase("24:00", TestName = "HouseRuleVersion_Create_WithHour24_Throws")]
    [TestCase("7:30", TestName = "HouseRuleVersion_Create_WithSingleDigitHour_Throws")]
    [TestCase("23:60", TestName = "HouseRuleVersion_Create_WithInvalidMinute_Throws")]
    [TestCase("12:5", TestName = "HouseRuleVersion_Create_WithSingleDigitMinute_Throws")]
    [TestCase("noon", TestName = "HouseRuleVersion_Create_WithWords_Throws")]
    public void HouseRuleVersion_Create_WithInvalidTimeOfDay_Throws(string time)
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => CreateRules(quietHoursStart: time), "quietHoursStart");
            Assert.Throws<DomainValidationException>(() => CreateRules(quietHoursEnd: time), "quietHoursEnd");
            Assert.Throws<DomainValidationException>(() => CreateRules(curfewTime: time), "curfewTime");
        });
    }

    [Test]
    public void HouseRuleVersion_Create_WithBlankOptionalFields_StoresNull()
    {
        var rules = CreateRules(quietHoursStart: " ", curfewTime: "", guestPolicyNote: "   ");

        Assert.Multiple(() =>
        {
            Assert.That(rules.QuietHoursStart, Is.Null);
            Assert.That(rules.CurfewTime, Is.Null);
            Assert.That(rules.GuestPolicyNote, Is.Null);
        });
    }

    [Test]
    public void HouseRuleVersion_Create_WithNotesAboveLimits_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() =>
                CreateRules(guestPolicyNote: new string('a', HouseRuleVersion.GuestPolicyNoteMaxLength + 1)));
            Assert.Throws<DomainValidationException>(() =>
                CreateRules(parkingRules: new string('a', HouseRuleVersion.ParkingRulesMaxLength + 1)));
            Assert.Throws<DomainValidationException>(() =>
                CreateRules(otherRules: new string('a', HouseRuleVersion.OtherRulesMaxLength + 1)));
        });
    }

    [Test]
    public void HouseRuleVersion_Create_WithNotesOnLimits_IsAccepted()
    {
        var rules = CreateRules(
            guestPolicyNote: new string('a', HouseRuleVersion.GuestPolicyNoteMaxLength),
            parkingRules: new string('b', HouseRuleVersion.ParkingRulesMaxLength),
            otherRules: new string('c', HouseRuleVersion.OtherRulesMaxLength));

        Assert.Multiple(() =>
        {
            Assert.That(rules.GuestPolicyNote, Has.Length.EqualTo(200));
            Assert.That(rules.ParkingRules, Has.Length.EqualTo(300));
            Assert.That(rules.OtherRules, Has.Length.EqualTo(1000));
        });
    }

    [Test]
    public void HouseRuleVersion_Create_WithUndefinedPolicies_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => HouseRuleVersion.Create(
                "room-1", 1, (GuestPolicy)99, CookingPolicy.Allowed, false, false, false, "landlord-1", FixedNow));
            Assert.Throws<DomainValidationException>(() => HouseRuleVersion.Create(
                "room-1", 1, GuestPolicy.Allowed, (CookingPolicy)99, false, false, false, "landlord-1", FixedNow));
        });
    }

    [TestCase(0, TestName = "HouseRuleVersion_Create_WithVersionZero_Throws")]
    [TestCase(-1, TestName = "HouseRuleVersion_Create_WithNegativeVersion_Throws")]
    public void HouseRuleVersion_Create_WithVersionBelowOne_Throws(int versionNumber)
    {
        Assert.Throws<DomainValidationException>(() => HouseRuleVersion.Create(
            "room-1", versionNumber, GuestPolicy.Allowed, CookingPolicy.Allowed, false, false, false, "landlord-1", FixedNow));
    }

    [Test]
    public void HouseRuleVersion_Create_WithBlankRoomOrAuthor_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => HouseRuleVersion.Create(
                "", 1, GuestPolicy.Allowed, CookingPolicy.Allowed, false, false, false, "landlord-1", FixedNow));
            Assert.Throws<DomainValidationException>(() => HouseRuleVersion.Create(
                "room-1", 1, GuestPolicy.Allowed, CookingPolicy.Allowed, false, false, false, " ", FixedNow));
        });
    }

    [Test]
    public void HouseRuleVersion_Reconstitute_StoresEveryErdColumn()
    {
        var rules = HouseRuleVersion.Reconstitute(
            id: "rule-1",
            roomId: "room-1",
            versionNumber: 3,
            guestPolicy: GuestPolicy.NotAllowed,
            cookingPolicy: CookingPolicy.NotAllowed,
            parkingAvailable: false,
            petsAllowed: true,
            smokingAllowed: true,
            createdByUserId: "landlord-1",
            createdAtUtc: FixedNow,
            quietHoursStart: "21:00",
            quietHoursEnd: "07:00",
            curfewTime: "22:30",
            guestPolicyNote: "Không tiếp khách",
            parkingRules: null,
            otherRules: "Giữ yên tĩnh");

        Assert.Multiple(() =>
        {
            Assert.That(rules.Id, Is.EqualTo("rule-1"));
            Assert.That(rules.RoomId, Is.EqualTo("room-1"));
            Assert.That(rules.VersionNumber, Is.EqualTo(3));
            Assert.That(rules.GuestPolicy, Is.EqualTo(GuestPolicy.NotAllowed));
            Assert.That(rules.CookingPolicy, Is.EqualTo(CookingPolicy.NotAllowed));
            Assert.That(rules.PetsAllowed, Is.True);
            Assert.That(rules.SmokingAllowed, Is.True);
            Assert.That(rules.QuietHoursStart, Is.EqualTo("21:00"));
            Assert.That(rules.QuietHoursEnd, Is.EqualTo("07:00"));
            Assert.That(rules.CurfewTime, Is.EqualTo("22:30"));
            Assert.That(rules.GuestPolicyNote, Is.EqualTo("Không tiếp khách"));
            Assert.That(rules.OtherRules, Is.EqualTo("Giữ yên tĩnh"));
            Assert.That(rules.CreatedAtUtc, Is.EqualTo(FixedNow));
        });
    }

    [Test]
    public void HouseRuleVersion_HasNoPublicParameterlessConstructor()
    {
        var publicParameterlessCtors = typeof(HouseRuleVersion)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length == 0)
            .ToList();

        Assert.That(publicParameterlessCtors, Is.Empty);
    }

    private static HouseRuleVersion CreateRules(
        string? quietHoursStart = "22:00",
        string? quietHoursEnd = "06:00",
        string? curfewTime = null,
        string? guestPolicyNote = null,
        string? parkingRules = null,
        string? otherRules = null) =>
        HouseRuleVersion.Create(
            "room-1", 1, GuestPolicy.Allowed, CookingPolicy.Allowed, true, false, false,
            "landlord-1", FixedNow, quietHoursStart, quietHoursEnd, curfewTime, guestPolicyNote, parkingRules, otherRules);
}
