using RoomToRentRadar.Application.UseCases.Landlord;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Landlord;

/// <summary>
/// UC-31 <c>getMyListing</c>: owner-scoped read (another landlord's room is 404 ROOM_NOT_FOUND),
/// ordered photos, current house rules and the ETag version of the listing.
/// </summary>
[TestFixture]
public class GetMyListingUseCaseTests
{
    private IRoomRepository _rooms = null!;
    private IRoomPhotoRepository _photos = null!;
    private GetMyListingUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _rooms = Substitute.For<IRoomRepository>();
        _photos = Substitute.For<IRoomPhotoRepository>();
        _useCase = new GetMyListingUseCase(_rooms, _photos, new FakePhotoStorage());
    }

    [Test]
    public async Task ExecuteAsync_OwnedRoom_ReturnsDetailWithChildrenAndOrderedPhotos()
    {
        var rules = AppTestData.HouseRulesVersion();
        var room = AppTestData.NewRoom(
            "room-1",
            status: ListingStatus.Available,
            version: 4,
            amenities: new[] { Amenity.Wifi },
            safetyFeatures: new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true) },
            houseRules: rules);
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(room);
        _photos
            .ListByRoomAsync("room-1", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                AppTestData.Attached(AppTestData.Photo("p2"), "room-1", sortOrder: 1),
                AppTestData.Attached(AppTestData.Photo("p1"), "room-1", sortOrder: 0)
            });

        var result = await _useCase.ExecuteAsync("landlord-1", "room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo("room-1"));
            Assert.That(result.Version, Is.EqualTo(4), "ETag = version (ADR-012).");
            Assert.That(result.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(result.Listing.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi }));
            Assert.That(result.Listing.SafetyFeatures, Is.EqualTo(new[] { SafetyFeature.Cctv }));
            Assert.That(result.Photos.Select(photo => photo.Id), Is.EqualTo(new[] { "p1", "p2" }),
                "Photos sắp theo SortOrder, 0 = cover.");
            Assert.That(result.HouseRules, Is.Not.Null);
            Assert.That(result.HouseRules!.VersionNumber, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_RoomOfAnotherLandlord_ThrowsRoomNotFound()
    {
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns((Room?)null);

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync("landlord-1", "room-1"),
            Throws.TypeOf<EntityNotFoundException>().With.Property("ErrorCode").EqualTo("ROOM_NOT_FOUND"));

        await _photos.DidNotReceiveWithAnyArgs().ListByRoomAsync(default!, default);
    }

    [TestCase(" ", "room-1")]
    [TestCase("landlord-1", " ")]
    public async Task ExecuteAsync_BlankIds_ThrowDomainValidation(string landlordId, string roomId)
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(landlordId, roomId),
            Throws.TypeOf<DomainValidationException>());
    }
}
