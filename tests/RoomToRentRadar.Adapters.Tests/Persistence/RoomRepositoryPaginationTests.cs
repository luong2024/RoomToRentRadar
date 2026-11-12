using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class RoomRepositoryPaginationTests
{
    private InMemoryRoomRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryRoomRepository();
        for (int i = 1; i <= 5; i++)
        {
            _repository.Upsert(new Room
            {
                Id = $"room-{i}",
                Title = $"Room {i}",
                Description = $"Description {i}",
                Price = 1_000_000m * i,
                Area = 20.0 + i,
                RoomType = "Single",
                IsVerified = true,
                Status = RoomStatus.Available,
                Address = "Test Address",
                Amenities = ["Wifi"],
                HouseRules = ["No smoking"]
            });
        }
    }

    [Test]
    public async Task SearchAsync_WhenPageNumberVeryLarge_ShouldNotThrowAndReturnEmpty()
    {
        var query = new SearchRoomsQuery(pageNumber: 100_000, pageSize: 50);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_WhenPageNumberCausesIntegerOverflow_ShouldNotThrowAndReturnEmpty()
    {
        var query = new SearchRoomsQuery(pageNumber: int.MaxValue, pageSize: 50);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_WhenOffsetExceedsTotalCount_ShouldReturnEmpty()
    {
        var query = new SearchRoomsQuery(pageNumber: 2, pageSize: 10);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_NormalPagination_ShouldReturnCorrectSlice()
    {
        var query = new SearchRoomsQuery(pageNumber: 1, pageSize: 2);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo("room-1"));
        Assert.That(items[1].Id, Is.EqualTo("room-2"));
    }
}
