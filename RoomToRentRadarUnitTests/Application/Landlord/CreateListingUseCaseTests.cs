using RoomToRentRadar.Application.UseCases.Landlord;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Landlord;

/// <summary>
/// UC-30 / US08 AC1–AC2: <c>createListing</c> enforces BR-LST-11, the BR-MED-01 photo mix and the
/// ownership/expiry rules of BR-MED-03, builds a PendingReview aggregate with house-rules v1 and
/// inserts room + attach in one unit of work.
/// </summary>
[TestFixture]
public class CreateListingUseCaseTests
{
    private IRoomRepository _rooms = null!;
    private IRoomPhotoRepository _photos = null!;
    private InlineUnitOfWork _unitOfWork = null!;
    private CreateListingUseCase _useCase = null!;
    private readonly Dictionary<string, RoomPhoto> _availablePhotos = new(StringComparer.Ordinal);
    private List<RoomPhoto> _storedPhotos = new();

    [SetUp]
    public void SetUp()
    {
        _rooms = Substitute.For<IRoomRepository>();
        _photos = Substitute.For<IRoomPhotoRepository>();
        _unitOfWork = new InlineUnitOfWork();
        _useCase = new CreateListingUseCase(_rooms, _photos, _unitOfWork, new FakePhotoStorage());
        _availablePhotos.Clear();
        _storedPhotos = new List<RoomPhoto>();

        _rooms.CountNonArchivedByLandlordAsync("landlord-1", Arg.Any<CancellationToken>()).Returns(0);
        _photos
            .ListOwnedByIdsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var landlordId = call.ArgAt<string>(0);
                var ids = call.ArgAt<IReadOnlyCollection<string>>(1);
                return Task.FromResult<IReadOnlyList<RoomPhoto>>(ids
                    .Where(_availablePhotos.ContainsKey)
                    .Select(id => _availablePhotos[id])
                    .Where(photo => photo.UploadedByUserId == landlordId)
                    .ToList());
            });
        _photos.AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _photos
            .AttachAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var roomId = call.ArgAt<string>(1);
                var ids = call.ArgAt<IReadOnlyList<string>>(2);
                _storedPhotos = ids
                    .Select((id, index) => AppTestData.Attached(_availablePhotos[id], roomId, index))
                    .ToList();
                return Task.CompletedTask;
            });
        _photos
            .ListByRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RoomPhoto>>(_storedPhotos));
    }

    [Test]
    public async Task ExecuteAsync_ValidRequest_CreatesPendingReviewWithChildrenAndHouseRulesV1()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard), ("p3", PhotoKind.Standard),
            ("p4", PhotoKind.Panorama));
        Room? added = null;
        _rooms.AddAsync(Arg.Do<Room>(room => added = room), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var command = new CreateListingCommand("landlord-1", AppTestData.ListingRequest(
            new[] { "p1", "p2", "p3", "p4" },
            amenities: new[] { Amenity.Wifi, Amenity.Bed },
            safetyFeatures: new[] { SafetyFeature.Cctv },
            houseRules: AppTestData.HouseRules()));

        var result = await _useCase.ExecuteAsync(command);

        Assert.That(added, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(added!.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(added.Status, Is.EqualTo(ListingStatus.PendingReview), "BR-LST-04: luôn chờ duyệt.");
            Assert.That(added.Version, Is.EqualTo(1));
            Assert.That(added.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.Bed }));
            Assert.That(added.SafetyFeatures, Is.EqualTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: false)
            }), "BR-LST-08: feature khai báo mới chưa được Admin xác nhận.");
            Assert.That(added.CurrentHouseRules, Is.Not.Null);
            Assert.That(added.CurrentHouseRules!.VersionNumber, Is.EqualTo(1));
            Assert.That(added.CurrentHouseRules.CreatedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(_storedPhotos.Select(photo => photo.Id), Is.EqualTo(new[] { "p1", "p2", "p3", "p4" }),
                "Thứ tự ảnh giữ nguyên.");
            Assert.That(_storedPhotos.Select(photo => photo.RoomId), Is.All.EqualTo(added.Id));
            Assert.That(_unitOfWork.Executions, Is.EqualTo(1), "Room + attach phải nằm trong một transaction.");
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(result.Version, Is.EqualTo(1));
            Assert.That(result.Listing.PhotoIds, Is.EqualTo(new[] { "p1", "p2", "p3", "p4" }));
            Assert.That(result.Listing.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.Bed }));
            Assert.That(result.Listing.SafetyFeatures, Is.EqualTo(new[] { SafetyFeature.Cctv }));
            Assert.That(result.Photos.Select(photo => photo.Id), Is.EqualTo(new[] { "p1", "p2", "p3", "p4" }));
            Assert.That(result.Photos.Select(photo => photo.SortOrder), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(result.HouseRules, Is.Not.Null);
            Assert.That(result.HouseRules!.VersionNumber, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_WithoutHouseRules_LeavesCurrentRulesNull()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard), ("p3", PhotoKind.Standard));
        Room? added = null;
        _rooms.AddAsync(Arg.Do<Room>(room => added = room), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _useCase.ExecuteAsync(
            new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2", "p3" })));

        Assert.Multiple(() =>
        {
            Assert.That(added!.CurrentHouseRules, Is.Null);
            Assert.That(result.HouseRules, Is.Null);
        });
    }

    [Test]
    public async Task ExecuteAsync_TwoStandardPhotos_ThrowsValidationFailed()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2" }))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));

        await _rooms.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        Assert.That(_unitOfWork.Executions, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_TwentyOneStandardPhotos_ThrowsValidationFailed()
    {
        var ids = Enumerable.Range(1, 21).Select(index => $"s{index}").ToArray();
        ArrangePhotos(ids.Select(id => (id, PhotoKind.Standard)).ToArray());

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(ids))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_FourPanoramas_ThrowsValidationFailed()
    {
        var ids = new[] { "s1", "s2", "s3", "p1", "p2", "p3", "p4" };
        ArrangePhotos(
            ("s1", PhotoKind.Standard), ("s2", PhotoKind.Standard), ("s3", PhotoKind.Standard),
            ("p1", PhotoKind.Panorama), ("p2", PhotoKind.Panorama), ("p3", PhotoKind.Panorama),
            ("p4", PhotoKind.Panorama));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(ids))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_PhotoOwnedByAnotherLandlord_ThrowsValidationFailed()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard));
        // p3 exists but belongs to someone else, so the ownership-scoped query does not return it.
        _availablePhotos["p3"] = AppTestData.Photo("p3", ownerId: "landlord-2");

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_PhotoAlreadyAttachedToARoom_ThrowsValidationFailed()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard));
        _availablePhotos["p3"] = AppTestData.Photo("p3", roomId: "room-other");

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_ExpiredUnattachedPhoto_ThrowsValidationFailed()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard));
        _availablePhotos["p3"] = AppTestData.Photo("p3", createdAtUtc: DateTime.UtcNow.AddHours(-25));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_DuplicatePhotoIds_ThrowsValidationFailed()
    {
        ArrangePhotos(("p1", PhotoKind.Standard), ("p2", PhotoKind.Standard));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2", "p1" }))),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_LandlordAtFiftyActiveListings_ThrowsLimitExceeded()
    {
        _rooms.CountNonArchivedByLandlordAsync("landlord-1", Arg.Any<CancellationToken>())
            .Returns(CreateListingUseCase.MaxActiveListings);

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(
                new CreateListingCommand("landlord-1", AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }))),
            Throws.TypeOf<LimitExceededException>().With.Property("ErrorCode").EqualTo("LIMIT_EXCEEDED"));

        await _photos.DidNotReceiveWithAnyArgs().ListOwnedByIdsAsync(default!, default!, default);
    }

    [Test]
    public async Task ExecuteAsync_MissingAddress_ThrowsValidationFailed()
    {
        var request = AppTestData.ListingRequest(new[] { "p1", "p2", "p3" }) with { Address = null! };

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(new CreateListingCommand("landlord-1", request)),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    private void ArrangePhotos(params (string Id, PhotoKind Kind)[] photos)
    {
        foreach (var (id, kind) in photos)
        {
            _availablePhotos[id] = AppTestData.Photo(id, kind: kind);
        }
    }
}
