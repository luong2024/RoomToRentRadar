using RoomToRentRadar.Application.UseCases.Landlord;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Landlord;

/// <summary>
/// UC-31 / SCR-LL-01: <c>listMyListings</c> forwards the paging/status filter, expands the cover thumb
/// path to a URL (null when the room has no photo) and always returns the six status counters.
/// </summary>
[TestFixture]
public class ListMyListingsUseCaseTests
{
    private IRoomRepository _rooms = null!;
    private ListMyListingsUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _rooms = Substitute.For<IRoomRepository>();
        _useCase = new ListMyListingsUseCase(_rooms, new FakePhotoStorage());
    }

    [Test]
    public async Task ExecuteAsync_MapsItemsCountsAndPaging()
    {
        var counts = Enum.GetValues<ListingStatus>().ToDictionary(status => status, _ => 0);
        counts[ListingStatus.Available] = 2;
        counts[ListingStatus.Rented] = 1;
        _rooms
            .ListByLandlordAsync("landlord-1", ListingStatus.Available, 2, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedOwnedRooms(
                new[]
                {
                    new OwnedRoomListing(
                        AppTestData.NewRoom("room-1", status: ListingStatus.Available),
                        "rooms/2026/11/photo-1/thumb.jpg")
                },
                TotalCount: 7,
                counts));

        var result = await _useCase.ExecuteAsync(new ListMyListingsRequest(
            "landlord-1", ListingStatus.Available, PageNumber: 2, PageSize: 5));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-1"));
            Assert.That(result.Items[0].Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(result.Items[0].CoverThumbUrl, Is.EqualTo("https://media.test/rooms/2026/11/photo-1/thumb.jpg"),
                "ERD §4.4: cover = ảnh SortOrder nhỏ nhất.");
            Assert.That(result.TotalCount, Is.EqualTo(7));
            Assert.That(result.PageNumber, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(5));
            Assert.That(result.CountsByStatus.Keys, Is.EquivalentTo(Enum.GetValues<ListingStatus>()),
                "SCR-LL-01: đủ 6 trạng thái, kể cả khi bằng 0.");
            Assert.That(result.CountsByStatus[ListingStatus.Available], Is.EqualTo(2));
            Assert.That(result.CountsByStatus[ListingStatus.Rented], Is.EqualTo(1));
            Assert.That(result.CountsByStatus[ListingStatus.Unpublished], Is.Zero);
        });
    }

    [Test]
    public async Task ExecuteAsync_RoomWithoutPhotos_HasNullCover()
    {
        var counts = Enum.GetValues<ListingStatus>().ToDictionary(status => status, _ => 0);
        _rooms
            .ListByLandlordAsync(Arg.Any<string>(), Arg.Any<ListingStatus?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedOwnedRooms(
                new[] { new OwnedRoomListing(AppTestData.NewRoom("room-1"), CoverThumbPath: null) },
                TotalCount: 1,
                counts));

        var result = await _useCase.ExecuteAsync(new ListMyListingsRequest("landlord-1"));

        Assert.That(result.Items[0].CoverThumbUrl, Is.Null, "BR-MED-05: phòng chưa có ảnh thì client hiện placeholder.");
    }

    [TestCase(0, 20)]
    [TestCase(-1, 20)]
    [TestCase(1, 0)]
    [TestCase(1, 51)]
    public async Task ExecuteAsync_InvalidPaging_ThrowsValidationFailed(int pageNumber, int pageSize)
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(new ListMyListingsRequest("landlord-1", null, pageNumber, pageSize)),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));

        await _rooms.DidNotReceiveWithAnyArgs().ListByLandlordAsync(default!, default, default, default, default);
    }

    [Test]
    public async Task ExecuteAsync_StatusOutsideTheWhitelist_ThrowsValidationFailed()
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(new ListMyListingsRequest("landlord-1", (ListingStatus)99)),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_BlankLandlord_ThrowsDomainValidation()
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(new ListMyListingsRequest(" ")),
            Throws.TypeOf<DomainValidationException>());
    }
}
