using System.Reflection;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>
/// H1 / BR-LST-01..06 / ADR-011: Room is an aggregate root whose status changes only through
/// domain methods and whose invariants reject invalid listings.
/// </summary>
[TestFixture]
public class RoomTests
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

    // ----------------------------------------------------------------- structure

    [Test]
    public void Room_LivesInAggregatesNamespace()
    {
        Assert.That(typeof(Room).Namespace, Is.EqualTo("RoomToRentRadar.Domain.Aggregates"));
    }

    [Test]
    public void Room_HasNoPublicParameterlessConstructor()
    {
        var publicParameterlessCtors = typeof(Room)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length == 0)
            .ToList();

        Assert.That(publicParameterlessCtors, Is.Empty,
            "Room phải được khởi tạo qua Room.Create hoặc Room.Reconstitute (Guardrail 1.5.3).");
    }

    [Test]
    public void Room_ConcurrencyToken_IsStringAndChangesOnTransition()
    {
        var room = CreateValidRoom();
        var tokenBefore = room.ConcurrencyToken;

        room.Approve(FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(room.ConcurrencyToken, Is.Not.Null.And.Not.Empty);
            Assert.That(room.ConcurrencyToken, Is.Not.EqualTo(tokenBefore));
            Assert.That(room.Version, Is.EqualTo(2));
            Assert.That(room.ConcurrencyToken, Is.EqualTo("2")); // ERD §1: token = Version.ToString()
        });
    }

    // ----------------------------------------------------------------- Create

    [Test]
    public void Create_WithValidData_StartsPendingReviewAndUnverified()
    {
        var room = CreateValidRoom();

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Has.Length.EqualTo(32)); // Guid "N"
            Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview)); // BR-LST-04
            Assert.That(room.VerifiedAtUtc, Is.Null);                          // BR-LST-06
            Assert.That(room.PublishedAtUtc, Is.Null);
            Assert.That(room.NeedsReReview, Is.False);
            Assert.That(room.IsSafetyVerified, Is.False);
            Assert.That(room.StatusReason, Is.Null);
            Assert.That(room.Version, Is.EqualTo(1));
            Assert.That(room.ConcurrencyToken, Is.EqualTo("1")); // ERD §1: token = Version.ToString()
            Assert.That(room.CreatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(room.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(room.LastUpdatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(room.MonthlyRentVnd, Is.EqualTo(1_500_000L));
            Assert.That(room.AreaM2, Is.EqualTo(18.0));
            Assert.That(room.RoomType, Is.EqualTo(RoomType.BoardingRoom));
        });
    }

    // ----------------------------------------------------------------- invariants (BR-LST-01)

    [TestCase("Ngắn", TestName = "Create_TitleBelow10Chars_Throws")]
    [TestCase("", TestName = "Create_EmptyTitle_Throws")]
    [TestCase("   ", TestName = "Create_WhitespaceTitle_Throws")]
    public void Create_TitleOutOfRange_Throws(string title)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", title, RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_TitleLongerThan100Chars_Throws()
    {
        var title = new string('a', 101);

        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", title, RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_TitleExactly100Chars_IsAccepted()
    {
        var title = new string('a', 100);

        var room = Room.Create(
            "landlord-1", title, RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080);

        Assert.That(room.Title, Has.Length.EqualTo(100));
    }

    [TestCase(99_999L, TestName = "Create_RentBelow100k_Throws")]
    [TestCase(50_000_001L, TestName = "Create_RentAbove50M_Throws")]
    public void Create_RentOutOfRange_Throws(long monthlyRentVnd)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, monthlyRentVnd, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_ZeroDeposit_IsAccepted()
    {
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080);

        Assert.That(room.DepositVnd, Is.Zero);
    }

    [Test]
    public void Create_DepositAboveThreeMonths_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 4_500_001L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_NegativeDeposit_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, -1L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [TestCase(4.9, TestName = "Create_AreaBelowMinimum_Throws")]
    [TestCase(200.1, TestName = "Create_AreaAboveMaximum_Throws")]
    [TestCase(double.NaN, TestName = "Create_AreaNaN_Throws")]
    [TestCase(double.PositiveInfinity, TestName = "Create_AreaInfinity_Throws")]
    public void Create_AreaOutOfRangeOrNotFinite_Throws(double areaM2)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, areaM2,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [TestCase(90.1, 105.8, TestName = "Create_LatitudeAbove90_Throws")]
    [TestCase(-90.1, 105.8, TestName = "Create_LatitudeBelowMinus90_Throws")]
    [TestCase(double.NaN, 105.8, TestName = "Create_LatitudeNaN_Throws")]
    [TestCase(double.PositiveInfinity, 105.8, TestName = "Create_LatitudeInfinity_Throws")]
    [TestCase(21.5, 180.1, TestName = "Create_LongitudeAbove180_Throws")]
    [TestCase(21.5, -180.1, TestName = "Create_LongitudeBelowMinus180_Throws")]
    [TestCase(21.5, double.NaN, TestName = "Create_LongitudeNaN_Throws")]
    [TestCase(21.5, double.NegativeInfinity, TestName = "Create_LongitudeNegativeInfinity_Throws")]
    public void Create_CoordinatesOutOfRangeOrNotFinite_Throws(double latitude, double longitude)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", latitude, longitude));
    }

    [Test]
    public void Create_WithEmptyRequiredTextFields_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080), "LandlordId");
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                " ", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080), "AddressLine");
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                "Z115", "", "TP. Thái Nguyên", 21.5875, 105.8080), "Ward");
            Assert.Throws<DomainValidationException>(() => Room.Create(
                "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
                "Z115", "Tân Thịnh", "  ", 21.5875, 105.8080), "District");
        });
    }

    [Test]
    public void Create_WithUndefinedRoomType_Throws()
    {
        Assert.Throws<DomainValidationException>(() => Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", (RoomType)99, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080));
    }

    [Test]
    public void Create_TrimsTitle()
    {
        var room = Room.Create(
            "landlord-1", "  Phòng trọ Sinh viên Z115  ", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080);

        Assert.That(room.Title, Is.EqualTo("Phòng trọ Sinh viên Z115"));
    }

    [Test]
    public void Create_WithLocalNow_StoresTimestampsInUtc()
    {
        var localNow = new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Local);

        var room = CreateValidRoom(localNow);

        Assert.Multiple(() =>
        {
            Assert.That(room.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(room.LastUpdatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(room.CreatedAtUtc, Is.EqualTo(localNow.ToUniversalTime()));
        });
    }

    [Test]
    public void Approve_WithLocalTimestamp_StoresUtc()
    {
        var localVerifiedAt = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Local);
        var room = CreateValidRoom();

        room.Approve(localVerifiedAt);

        Assert.Multiple(() =>
        {
            Assert.That(room.VerifiedAtUtc, Is.EqualTo(localVerifiedAt.ToUniversalTime()));
            Assert.That(room.VerifiedAtUtc!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(room.PublishedAtUtc, Is.EqualTo(localVerifiedAt.ToUniversalTime()));
        });
    }

    // ----------------------------------------------------------------- Reconstitute

    [Test]
    public void Reconstitute_StoresEveryErdColumn()
    {
        var createdAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var verifiedAt = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

        var room = Room.Reconstitute(
            id: "room-1",
            landlordId: "landlord-1",
            title: "Phòng trọ Sinh viên Z115",
            roomType: RoomType.MiniApartment,
            monthlyRentVnd: 2_800_000L,
            depositVnd: 5_600_000L,
            areaM2: 28.5,
            addressLine: "Ngõ 12",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            status: ListingStatus.Available,
            createdAtUtc: createdAt,
            lastUpdatedAtUtc: createdAt,
            version: 3,
            concurrencyToken: "token-3",
            description: "Mô tả",
            maxOccupants: 3,
            furnishing: "Full",
            availableFrom: new DateOnly(2026, 12, 1),
            electricityPriceVndPerKwh: 3_500L,
            waterPriceVnd: 100_000L,
            waterPriceUnit: "PerPerson",
            internetFeeVnd: 100_000L,
            parkingFeeVnd: 50_000L,
            otherFeesNote: "Không",
            statusReason: null,
            needsReReview: true,
            isSafetyVerified: true,
            verifiedAtUtc: verifiedAt,
            verifiedByUserId: "admin-1",
            publishedAtUtc: verifiedAt);

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.EqualTo("room-1"));
            Assert.That(room.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(room.RoomType, Is.EqualTo(RoomType.MiniApartment));
            Assert.That(room.MonthlyRentVnd, Is.EqualTo(2_800_000L));
            Assert.That(room.DepositVnd, Is.EqualTo(5_600_000L));
            Assert.That(room.AreaM2, Is.EqualTo(28.5));
            Assert.That(room.AddressLine, Is.EqualTo("Ngõ 12"));
            Assert.That(room.Ward, Is.EqualTo("Tân Thịnh"));
            Assert.That(room.District, Is.EqualTo("TP. Thái Nguyên"));
            Assert.That(room.MaxOccupants, Is.EqualTo(3));
            Assert.That(room.Furnishing, Is.EqualTo("Full"));
            Assert.That(room.AvailableFrom, Is.EqualTo(new DateOnly(2026, 12, 1)));
            Assert.That(room.ElectricityPriceVndPerKwh, Is.EqualTo(3_500L));
            Assert.That(room.WaterPriceVnd, Is.EqualTo(100_000L));
            Assert.That(room.WaterPriceUnit, Is.EqualTo("PerPerson"));
            Assert.That(room.InternetFeeVnd, Is.EqualTo(100_000L));
            Assert.That(room.ParkingFeeVnd, Is.EqualTo(50_000L));
            Assert.That(room.OtherFeesNote, Is.EqualTo("Không"));
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(room.NeedsReReview, Is.True);
            Assert.That(room.IsSafetyVerified, Is.True);
            Assert.That(room.VerifiedAtUtc, Is.EqualTo(verifiedAt));
            Assert.That(room.VerifiedByUserId, Is.EqualTo("admin-1"));
            Assert.That(room.PublishedAtUtc, Is.EqualTo(verifiedAt));
            Assert.That(room.CreatedAtUtc, Is.EqualTo(createdAt));
            Assert.That(room.Version, Is.EqualTo(3));
            Assert.That(room.ConcurrencyToken, Is.EqualTo("token-3"));
        });
    }

    [Test]
    public void Reconstitute_WithoutConcurrencyToken_FallsBackToVersion()
    {
        var room = Room.Reconstitute(
            "room-1", "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom,
            1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
            ListingStatus.Available, FixedNow, FixedNow, version: 4);

        Assert.That(room.ConcurrencyToken, Is.EqualTo("4"));
    }

    [Test]
    public void Reconstitute_WithInvalidData_StillValidatesInvariants()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => Room.Reconstitute(
                "room-1", "landlord-1", "Ngắn", RoomType.BoardingRoom,
                1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
                ListingStatus.Available, FixedNow, FixedNow, version: 1), "Title");
            Assert.Throws<DomainValidationException>(() => Room.Reconstitute(
                "room-1", "landlord-1", "Phòng trọ Sinh viên Z115", (RoomType)99,
                1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
                ListingStatus.Available, FixedNow, FixedNow, version: 1), "RoomType");
            Assert.Throws<DomainValidationException>(() => Room.Reconstitute(
                "room-1", "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom,
                1_500_000L, 0L, 18.0, "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080,
                ListingStatus.Available, FixedNow, FixedNow, version: 0), "Version");
        });
    }

    // ----------------------------------------------------------------- ADR-011 state machine

    [Test]
    public void Approve_FromPendingReview_MovesToAvailableAndSetsVerifiedAt()
    {
        var room = CreateValidRoom();

        room.Approve(FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(room.VerifiedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(room.PublishedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(room.StatusReason, Is.Null);
            Assert.That(room.Version, Is.EqualTo(2));
            Assert.That(room.IsPubliclyVisible, Is.True);
        });
    }

    [Test]
    public void Approve_AfterRejectionAndResubmit_DoesNotOverwriteFirstPublishedAt()
    {
        var firstApproval = FixedNow;
        var secondApproval = FixedNow.AddDays(3);
        var room = CreateValidRoom();
        room.Approve(firstApproval);
        room.Suspend("Bị báo cáo");
        room.Resubmit();
        room.Approve(secondApproval);

        Assert.Multiple(() =>
        {
            Assert.That(room.PublishedAtUtc, Is.EqualTo(firstApproval));
            Assert.That(room.VerifiedAtUtc, Is.EqualTo(secondApproval));
        });
    }

    [Test]
    public void Reject_FromPendingReview_SetsStatusReason()
    {
        var room = CreateValidRoom();

        room.Reject("Thiếu ảnh thực tế");

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Rejected));
            Assert.That(room.StatusReason, Is.EqualTo("Thiếu ảnh thực tế"));
            Assert.That(room.Version, Is.EqualTo(2));
            Assert.That(room.IsPubliclyVisible, Is.False);
        });
    }

    [Test]
    public void Reject_WithBlankReason_ThrowsDomainValidationException()
    {
        var room = CreateValidRoom();

        Assert.Throws<DomainValidationException>(() => room.Reject("  "));
        Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public void Resubmit_FromRejected_ReturnsToPendingReviewAndClearsReason()
    {
        var room = CreateRoomInStatus(ListingStatus.Rejected);

        room.Resubmit();

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(room.StatusReason, Is.Null);
        });
    }

    [Test]
    public void Resubmit_FromSuspended_ReturnsToPendingReviewAndClearsReason()
    {
        var room = CreateRoomInStatus(ListingStatus.Suspended);

        room.Resubmit();

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(room.StatusReason, Is.Null);
        });
    }

    [Test]
    public void MarkRented_ThenMarkAvailable_TogglesBetweenRentedAndAvailable()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.MarkRented();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Rented));

        room.MarkAvailable();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
    }

    [Test]
    public void Unpublish_ThenRepublish_RestoresAvailable()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.Unpublish();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Unpublished));

        room.Republish();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
    }

    [Test]
    public void Unpublish_FromRented_IsAllowed()
    {
        var room = CreateRoomInStatus(ListingStatus.Rented);

        room.Unpublish();

        Assert.That(room.Status, Is.EqualTo(ListingStatus.Unpublished));
    }

    [Test]
    public void Suspend_FromAvailable_RentsSetsReasonAndBlocksVisibility()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        room.Suspend("Bị báo cáo lừa đảo");

        Assert.Multiple(() =>
        {
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Suspended));
            Assert.That(room.StatusReason, Is.EqualTo("Bị báo cáo lừa đảo"));
            Assert.That(room.IsPubliclyVisible, Is.False);
        });
    }

    [Test]
    public void HappyPathTransitions_BumpVersionAndRotateConcurrencyToken()
    {
        var room = CreateValidRoom();
        var seenTokens = new HashSet<string> { room.ConcurrencyToken };
        var version = room.Version;

        var transitions = new (string Name, Action<Room> Apply)[]
        {
            ("Approve", r => r.Approve(FixedNow)),
            ("MarkRented", r => r.MarkRented()),
            ("MarkAvailable", r => r.MarkAvailable()),
            ("Unpublish", r => r.Unpublish()),
            ("Republish", r => r.Republish()),
            ("Suspend", r => r.Suspend("Vi phạm")),
            ("Resubmit", r => r.Resubmit()),
            ("ApproveAgain", r => r.Approve(FixedNow.AddDays(1))),
            ("UnpublishAgain", r => r.Unpublish()),
        };

        foreach (var (name, apply) in transitions)
        {
            apply(room);
            version++;

            Assert.Multiple(() =>
            {
                Assert.That(room.Version, Is.EqualTo(version), name);
                Assert.That(seenTokens.Add(room.ConcurrencyToken), Is.True, $"{name} phải tạo token mới");
                Assert.That(room.ConcurrencyToken, Is.EqualTo(version.ToString()), $"{name} token phải khớp Version");
            });
        }
    }

    [TestCase("MarkRented")]
    [TestCase("MarkAvailable")]
    [TestCase("Unpublish")]
    [TestCase("Republish")]
    [TestCase("Suspend")]
    [TestCase("Resubmit")]
    public void PendingReview_IllegalTransition_ThrowsInvalidStateTransitionException(string operation)
    {
        var room = CreateRoomInStatus(ListingStatus.PendingReview);

        Assert.Throws<InvalidStateTransitionException>(() => Apply(room, operation));
        Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public void Approve_FromEveryNonPendingState_IsRejected()
    {
        var nonPending = new[]
        {
            ListingStatus.Available,
            ListingStatus.Rejected,
            ListingStatus.Rented,
            ListingStatus.Unpublished,
            ListingStatus.Suspended
        };

        foreach (var status in nonPending)
        {
            var room = CreateRoomInStatus(status);
            Assert.Throws<InvalidStateTransitionException>(() => room.Approve(FixedNow), status.ToString());
        }
    }

    [Test]
    public void Reject_FromNonPendingState_IsRejected()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        Assert.Throws<InvalidStateTransitionException>(() => room.Reject("Thiếu ảnh"));
    }

    [Test]
    public void MarkRented_FromNonAvailableState_IsRejected()
    {
        var room = CreateRoomInStatus(ListingStatus.PendingReview);

        Assert.Throws<InvalidStateTransitionException>(() => room.MarkRented());
    }

    [Test]
    public void MarkAvailable_FromNonRentedState_IsRejected()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        Assert.Throws<InvalidStateTransitionException>(() => room.MarkAvailable());
    }

    [Test]
    public void Republish_FromNonUnpublishedState_IsRejected()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        Assert.Throws<InvalidStateTransitionException>(() => room.Republish());
    }

    [Test]
    public void Suspend_FromPendingReviewOrRejected_IsRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidStateTransitionException>(
                () => CreateRoomInStatus(ListingStatus.PendingReview).Suspend("vi phạm"));
            Assert.Throws<InvalidStateTransitionException>(
                () => CreateRoomInStatus(ListingStatus.Rejected).Suspend("vi phạm"));
        });
    }

    [Test]
    public void Resubmit_FromNonRejectedOrSuspended_IsRejected()
    {
        var room = CreateRoomInStatus(ListingStatus.Available);

        Assert.Throws<InvalidStateTransitionException>(() => room.Resubmit());
    }

    private static void Apply(Room room, string operation)
    {
        switch (operation)
        {
            case "Approve": room.Approve(FixedNow); break;
            case "Reject": room.Reject("Vi phạm"); break;
            case "Resubmit": room.Resubmit(); break;
            case "MarkRented": room.MarkRented(); break;
            case "MarkAvailable": room.MarkAvailable(); break;
            case "Unpublish": room.Unpublish(); break;
            case "Republish": room.Republish(); break;
            case "Suspend": room.Suspend("Vi phạm"); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }
}
