using RoomToRentRadar.Application.UseCases.Landlord;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Landlord;

/// <summary>
/// UC-32 / US08 AC3: <c>updateListing</c> requires If-Match (428/412), scopes the load to the owner
/// (404 anti-IDOR), re-validates the photo mix, ignores <c>houseRules</c> and keeps the BR-LST-05
/// NeedsReReview semantics (reorder = no re-review, new photo = re-review).
/// </summary>
[TestFixture]
public class UpdateListingUseCaseTests
{
    private IRoomRepository _rooms = null!;
    private IRoomPhotoRepository _photos = null!;
    private InlineUnitOfWork _unitOfWork = null!;
    private FakePhotoStorage _storage = null!;
    private ITravelTimeCacheRepository _travelTimeCache = null!;
    private UpdateListingUseCase _useCase = null!;
    private readonly Dictionary<string, RoomPhoto> _photosById = new(StringComparer.Ordinal);
    private Room _room = null!;
    private IReadOnlyList<RoomPhoto> _removedPhotos = Array.Empty<RoomPhoto>();
    private List<RoomPhoto> _storedPhotos = new();

    [SetUp]
    public void SetUp()
    {
        _rooms = Substitute.For<IRoomRepository>();
        _photos = Substitute.For<IRoomPhotoRepository>();
        _unitOfWork = new InlineUnitOfWork();
        _storage = new FakePhotoStorage();
        _travelTimeCache = Substitute.For<ITravelTimeCacheRepository>();
        _useCase = new UpdateListingUseCase(_rooms, _photos, _unitOfWork, _storage, _travelTimeCache);

        _room = AppTestData.NewRoom("room-1", status: ListingStatus.Available, version: 3);
        _photosById.Clear();
        _removedPhotos = Array.Empty<RoomPhoto>();
        _storedPhotos = new List<RoomPhoto>();

        ArrangeAttached("p1", 0);
        ArrangeAttached("p2", 1);
        ArrangeAttached("p3", 2);

        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);
        _rooms.UpdateAsync(Arg.Any<Room>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _photos
            .ListOwnedByIdsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var landlordId = call.Arg<string>();
                var ids = call.Arg<IReadOnlyCollection<string>>();
                return Task.FromResult<IReadOnlyList<RoomPhoto>>(ids
                    .Where(_photosById.ContainsKey)
                    .Select(id => _photosById[id])
                    .Where(photo => photo.UploadedByUserId == landlordId)
                    .ToList());
            });
        _photos
            .RemoveNotInListAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(_removedPhotos));
        _photos
            .AttachAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(ids =>
                _storedPhotos = ids
                    .Select((id, index) => AppTestData.Attached(_photosById[id], "room-1", index))
                    .ToList()), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _photos
            .ListByRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RoomPhoto>>(_storedPhotos));
    }

    [Test]
    public async Task ExecuteAsync_MissingIfMatch_ThrowsIfMatchRequired()
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(Command(ifMatch: null, "p1", "p2", "p3")),
            Throws.TypeOf<IfMatchRequiredException>());

        await _rooms.DidNotReceiveWithAnyArgs().GetOwnedByIdAsync(default!, default!, default);
        Assert.That(_unitOfWork.Executions, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_NonNumericIfMatch_ThrowsEtagMismatch()
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(Command(ifMatch: "not-a-version", "p1", "p2", "p3")),
            Throws.TypeOf<EtagMismatchException>());
    }

    [Test]
    public async Task ExecuteAsync_StaleIfMatch_ThrowsEtagMismatch()
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(Command(ifMatch: "\"2\"", "p1", "p2", "p3")),
            Throws.TypeOf<EtagMismatchException>());

        await _rooms.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_RoomOfAnotherLandlord_ThrowsRoomNotFound()
    {
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns((Room?)null);

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3")),
            Throws.TypeOf<EntityNotFoundException>().With.Property("ErrorCode").EqualTo("ROOM_NOT_FOUND"));
    }

    [Test]
    public async Task ExecuteAsync_ReorderedPhotos_DoesNotRaiseNeedsReReview()
    {
        var result = await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p3", "p1", "p2"));

        Assert.Multiple(() =>
        {
            Assert.That(_room.NeedsReReview, Is.False, "BR-LST-05: đổi thứ tự ảnh không phải thay đổi material.");
            Assert.That(result.Listing.PhotoIds, Is.EqualTo(new[] { "p3", "p1", "p2" }));
            Assert.That(result.Photos.Select(photo => photo.SortOrder), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.Version, Is.EqualTo(4));
            Assert.That(_unitOfWork.Executions, Is.EqualTo(1));
        });

        await _photos.Received(1).AttachAsync(
            "landlord-1", "room-1",
            Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "p3", "p1", "p2" })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_NewPhotoRaisedNeedsReReview()
    {
        _photosById["p4"] = AppTestData.Photo("p4");

        var result = await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3", "p4"));

        Assert.Multiple(() =>
        {
            Assert.That(_room.NeedsReReview, Is.True,
                "BR-LST-05: gắn ảnh mới vào phòng Available phải đưa tin về hàng chờ duyệt lại.");
            Assert.That(result.Listing.PhotoIds, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public async Task ExecuteAsync_DroppedPhotos_AreRemovedAndTheirFilesDeletedAfterCommit()
    {
        var dropped = AppTestData.Attached(AppTestData.Photo("p4"), "room-1", 3);
        _photosById["p4"] = dropped;
        _removedPhotos = new[] { dropped };

        var result = await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Listing.PhotoIds, Is.EqualTo(new[] { "p1", "p2", "p3" }));
            Assert.That(_storage.DeletedKeys, Is.EquivalentTo(new[]
            {
                "rooms/2026/11/p4/original.jpg",
                "rooms/2026/11/p4/medium.jpg",
                "rooms/2026/11/p4/thumb.jpg"
            }));
        });

        await _photos.Received(1).RemoveNotInListAsync(
            "room-1",
            Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "p1", "p2", "p3" })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_HouseRulesInPayload_AreIgnored()
    {
        var existingRules = AppTestData.HouseRulesVersion();
        _room = AppTestData.NewRoom(
            "room-1", status: ListingStatus.Available, version: 3, houseRules: existingRules);
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);

        var payloadRules = AppTestData.HouseRules() with { GuestPolicy = GuestPolicy.NotAllowed };
        var command = Command(ifMatch: "\"3\"", "p1", "p2", "p3") with
        {
            Listing = AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }, houseRules: payloadRules)
        };

        var result = await _useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(_room.CurrentHouseRules, Is.SameAs(existingRules),
                "BR-HR-02: updateListing bỏ qua houseRules; current version giữ nguyên.");
            Assert.That(result.HouseRules, Is.Not.Null);
            Assert.That(result.HouseRules!.VersionNumber, Is.EqualTo(1));
            Assert.That(result.HouseRules.GuestPolicy, Is.EqualTo(GuestPolicy.DaytimeOnly));
        });
    }

    [Test]
    public async Task ExecuteAsync_RejectedListing_ReturnsToPendingReviewAndClearsTheReason()
    {
        _room = AppTestData.NewRoom(
            "room-1", status: ListingStatus.Rejected, version: 3, statusReason: "Ảnh mờ, thiếu thông tin.");
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);

        var result = await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ListingStatus.PendingReview),
                "BR-LST-05: sửa tin Rejected đưa tin về PendingReview.");
            Assert.That(result.StatusReason, Is.Null);
        });
    }

    [Test]
    public async Task ExecuteAsync_SuspendedListing_StaysSuspendedUntilResubmit()
    {
        _room = AppTestData.NewRoom(
            "room-1", status: ListingStatus.Suspended, version: 3, statusReason: "Khiếu nại đang xử lý.");
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);

        var result = await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ListingStatus.Suspended),
                "BR-LST-04: Suspended chỉ đổi qua resubmit, sửa nội dung không đổi trạng thái.");
            Assert.That(result.StatusReason, Is.EqualTo("Khiếu nại đang xử lý."));
        });
    }

    [Test]
    public async Task ExecuteAsync_PhotoAlreadyAttachedToAnotherRoom_ThrowsValidationFailed()
    {
        _photosById["p4"] = AppTestData.Attached(AppTestData.Photo("p4"), "room-other", 0);

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3", "p4")),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_UpdateAndAttachRunInsideOneUnitOfWork()
    {
        await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3"));

        Assert.That(_unitOfWork.Executions, Is.EqualTo(1));
        await _rooms.Received(1).UpdateAsync(_room, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_UndefinedFurnishingEnum_ThrowsDomainValidationException()
    {
        // Room.UpdateDetails re-runs EnsureInvariants, so an out-of-range enum is rejected on edit too
        // (GUARDRAILS 1.5.1 -> INVALID_FURNISHING) and nothing is written.
        var command = Command(ifMatch: "\"3\"", "p1", "p2", "p3") with
        {
            Listing = AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }) with { Furnishing = (Furnishing)999 }
        };

        var exception = Assert.ThrowsAsync<DomainValidationException>(
            async () => await _useCase.ExecuteAsync(command));

        Assert.That(exception!.ErrorCode, Is.EqualTo("INVALID_FURNISHING"));
        await _rooms.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        Assert.That(_unitOfWork.Executions, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_WhenPinCoordinatesChange_InvalidatesTravelTimeCacheInsideUnitOfWork()
    {
        var movedRequest = AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }) with
        {
            Location = new RoomToRentRadar.Ports.Contracts.Responses.Rooms.GeoPointDto(_room.Latitude + 0.005, _room.Longitude + 0.005)
        };
        var command = new UpdateListingCommand("landlord-1", "room-1", "\"3\"", movedRequest);

        await _useCase.ExecuteAsync(command);

        await _travelTimeCache.Received(1).InvalidateByRoomIdAsync("room-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenPinCoordinatesUnchanged_DoesNotInvalidateTravelTimeCache()
    {
        await _useCase.ExecuteAsync(Command(ifMatch: "\"3\"", "p1", "p2", "p3"));

        await _travelTimeCache.DidNotReceiveWithAnyArgs().InvalidateByRoomIdAsync(default!, default);
    }

    private UpdateListingCommand Command(string? ifMatch, params string[] photoIds) =>
        new("landlord-1", "room-1", ifMatch, AppTestData.ListingRequest(photoIds));

    private void ArrangeAttached(string id, int sortOrder) =>
        _photosById[id] = AppTestData.Attached(AppTestData.Photo(id), "room-1", sortOrder);
}
