using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class RoomsContractsTests
{
    [Test]
    public void RoomSearchRequest_HasExpectedDefaults()
    {
        var request = new RoomSearchRequest();

        Assert.Multiple(() =>
        {
            Assert.That(request.MinPrice, Is.Null);
            Assert.That(request.MaxPrice, Is.Null);
            Assert.That(request.MinArea, Is.Null);
            Assert.That(request.MaxArea, Is.Null);
            Assert.That(request.Latitude, Is.Null);
            Assert.That(request.Longitude, Is.Null);
            Assert.That(request.MaxDistanceKm, Is.Null);
            Assert.That(request.Keyword, Is.Null);
            Assert.That(request.SortBy, Is.Null);
            Assert.That(request.Page, Is.EqualTo(1));
            Assert.That(request.PageSize, Is.EqualTo(10));
        });
    }

    [Test]
    public void RoomSearchRequest_WithExpression_ProducesClonedValue()
    {
        var request = new RoomSearchRequest(Page: 1, PageSize: 10);

        var clamped = request with { Page = 2, PageSize = 50 };

        Assert.Multiple(() =>
        {
            Assert.That(clamped.Page, Is.EqualTo(2));
            Assert.That(clamped.PageSize, Is.EqualTo(50));
            Assert.That(request.Page, Is.EqualTo(1));
        });
    }

    [Test]
    public void RoomCardDto_StoresAllValues()
    {
        var createdAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var dto = new RoomCardDto(
            "room-1", "Title", "Desc", 1_000_000m, 20.0, "Addr",
            21.5851, 105.8066, 1.25, true, true, createdAt);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo("room-1"));
            Assert.That(dto.Price, Is.EqualTo(1_000_000m));
            Assert.That(dto.Area, Is.EqualTo(20.0));
            Assert.That(dto.DistanceKm, Is.EqualTo(1.25));
            Assert.That(dto.IsAvailable, Is.True);
            Assert.That(dto.IsVerified, Is.True);
            Assert.That(dto.CreatedAtUtc, Is.EqualTo(createdAt));
        });
    }

    [Test]
    public async Task IRoomRepository_CanBeSubstitutedWithNSubstitute()
    {
        var repository = Substitute.For<IRoomRepository>();
        var room = Room.Create(
            title: "Phòng hợp đồng",
            description: "Mô tả",
            price: 1_000_000m,
            area: 20,
            address: "Tân Thịnh, Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            landlordId: "landlord-1");

        repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Room> { room });
        repository.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);

        var all = await repository.GetAllAsync();
        var found = await repository.GetByIdAsync(room.Id);

        Assert.Multiple(() =>
        {
            Assert.That(all, Has.Count.EqualTo(1));
            Assert.That(found, Is.SameAs(room));
        });
    }

    [Test]
    public async Task ISearchRoomsUseCase_CanBeSubstitutedWithNSubstitute()
    {
        var useCase = Substitute.For<ISearchRoomsUseCase>();
        var expected = new PagedResult<RoomCardDto>(Array.Empty<RoomCardDto>(), 0, 1, 10);

        useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await useCase.ExecuteAsync(new RoomSearchRequest());

        Assert.That(result, Is.SameAs(expected));
    }
}
