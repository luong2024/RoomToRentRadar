using RoomToRentRadar.Application.UseCases.Landlord;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Landlord;

/// <summary>
/// UC-33 / BR-LST-04: the five landlord transitions share If-Match (428/412), owner scoping (404) and
/// the ADR-011 state machine (409 INVALID_STATE_TRANSITION); each success persists the aggregate and
/// returns the new ETag version.
/// </summary>
[TestFixture]
public class TransitionMyListingUseCaseTests
{
    private IRoomRepository _rooms = null!;
    private IRoomPhotoRepository _photos = null!;
    private InlineUnitOfWork _unitOfWork = null!;
    private Room _room = null!;

    [SetUp]
    public void SetUp()
    {
        _rooms = Substitute.For<IRoomRepository>();
        _photos = Substitute.For<IRoomPhotoRepository>();
        _unitOfWork = new InlineUnitOfWork();
        _room = AppTestData.NewRoom("room-1", status: ListingStatus.Available, version: 2);
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);
        _rooms.UpdateAsync(Arg.Any<Room>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _photos
            .ListByRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomPhoto>());
    }

    [TestCase("mark-rented", ListingStatus.Available, ListingStatus.Rented)]
    [TestCase("mark-available", ListingStatus.Rented, ListingStatus.Available)]
    [TestCase("unpublish", ListingStatus.Available, ListingStatus.Unpublished)]
    [TestCase("unpublish", ListingStatus.Rented, ListingStatus.Unpublished)]
    [TestCase("republish", ListingStatus.Unpublished, ListingStatus.Available)]
    [TestCase("resubmit", ListingStatus.Rejected, ListingStatus.PendingReview)]
    [TestCase("resubmit", ListingStatus.Suspended, ListingStatus.PendingReview)]
    public async Task ExecuteAsync_ValidTransition_PersistsAndReturnsNewStatus(
        string operation,
        ListingStatus from,
        ListingStatus to)
    {
        _room = AppTestData.NewRoom("room-1", status: from, version: 2);
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);

        var result = await ExecuteAsync(operation, ifMatch: "\"2\"");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(to));
            Assert.That(result.Version, Is.EqualTo(3));
            Assert.That(_room.Status, Is.EqualTo(to));
            Assert.That(_unitOfWork.Executions, Is.EqualTo(1), "Lưu aggregate chạy trong một transaction.");
        });

        await _rooms.Received(1).UpdateAsync(_room, Arg.Any<CancellationToken>());
    }

    [TestCase("mark-rented", ListingStatus.PendingReview)]
    [TestCase("mark-available", ListingStatus.Available)]
    [TestCase("unpublish", ListingStatus.PendingReview)]
    [TestCase("republish", ListingStatus.Rented)]
    [TestCase("resubmit", ListingStatus.Available)]
    public async Task ExecuteAsync_WrongStartingState_ThrowsInvalidStateTransition(
        string operation,
        ListingStatus from)
    {
        _room = AppTestData.NewRoom("room-1", status: from, version: 2);
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns(_room);

        await Assert.ThatAsync(
            async () => await ExecuteAsync(operation, ifMatch: "\"2\""),
            Throws.TypeOf<InvalidStateTransitionException>());

        await _rooms.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_MissingIfMatch_ThrowsIfMatchRequired()
    {
        await Assert.ThatAsync(
            async () => await ExecuteAsync("mark-rented", ifMatch: null),
            Throws.TypeOf<IfMatchRequiredException>());

        await _rooms.DidNotReceiveWithAnyArgs().GetOwnedByIdAsync(default!, default!, default);
    }

    [Test]
    public async Task ExecuteAsync_StaleIfMatch_ThrowsEtagMismatch()
    {
        await Assert.ThatAsync(
            async () => await ExecuteAsync("mark-rented", ifMatch: "\"1\""),
            Throws.TypeOf<EtagMismatchException>());

        await _rooms.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_RoomOfAnotherLandlord_ThrowsRoomNotFound()
    {
        _rooms.GetOwnedByIdAsync("room-1", "landlord-1", Arg.Any<CancellationToken>()).Returns((Room?)null);

        await Assert.ThatAsync(
            async () => await ExecuteAsync("unpublish", ifMatch: "\"2\""),
            Throws.TypeOf<EntityNotFoundException>().With.Property("ErrorCode").EqualTo("ROOM_NOT_FOUND"));
    }

    private Task<MyListingDetailDto> ExecuteAsync(string operation, string? ifMatch)
    {
        var command = new TransitionMyListingCommand("landlord-1", "room-1", ifMatch);
        var storage = new FakePhotoStorage();

        return operation switch
        {
            "mark-rented" => new MarkListingRentedUseCase(_rooms, _photos, _unitOfWork, storage).ExecuteAsync(command),
            "mark-available" => new MarkListingAvailableUseCase(_rooms, _photos, _unitOfWork, storage).ExecuteAsync(command),
            "unpublish" => new UnpublishListingUseCase(_rooms, _photos, _unitOfWork, storage).ExecuteAsync(command),
            "republish" => new RepublishListingUseCase(_rooms, _photos, _unitOfWork, storage).ExecuteAsync(command),
            "resubmit" => new ResubmitListingUseCase(_rooms, _photos, _unitOfWork, storage).ExecuteAsync(command),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown transition.")
        };
    }
}
