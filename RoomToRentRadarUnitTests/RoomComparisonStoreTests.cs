using RoomToRentRadar.Features.Comparison.Models;
using RoomToRentRadar.Features.Comparison.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomComparisonStoreTests
{
    [Test]
    public void TryAdd_WhenUnderLimit_AddsRoom()
    {
        var store = new RoomComparisonStore();

        var result = store.TryAdd(CreateCandidate("A"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomComparisonAddResult.Added));
            Assert.That(store.Rooms, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TryAdd_WhenRoomAlreadyCompared_ReturnsAlreadyComparedWithoutDuplicating()
    {
        var store = new RoomComparisonStore();
        var candidate = CreateCandidate("A");
        store.TryAdd(candidate);

        var result = store.TryAdd(candidate);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomComparisonAddResult.AlreadyCompared));
            Assert.That(store.Rooms, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TryAdd_WhenThreeRoomsPresent_ReturnsLimitReachedAndKeepsExisting()
    {
        var store = new RoomComparisonStore();
        store.TryAdd(CreateCandidate("A"));
        store.TryAdd(CreateCandidate("B"));
        store.TryAdd(CreateCandidate("C"));

        var result = store.TryAdd(CreateCandidate("D"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomComparisonAddResult.LimitReached));
            Assert.That(store.Rooms, Has.Count.EqualTo(3));
            Assert.That(store.Contains(CreateCandidate("D").Id), Is.False);
        });
    }

    [Test]
    public void Contains_WhenRoomWasRemoved_ReturnsFalse()
    {
        var store = new RoomComparisonStore();
        var candidate = CreateCandidate("A");
        store.TryAdd(candidate);

        var removed = store.Remove(candidate.Id);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(store.Contains(candidate.Id), Is.False);
        });
    }

    [Test]
    public void Remove_WhenRoomDoesNotExist_ReturnsFalse()
    {
        var store = new RoomComparisonStore();

        Assert.That(store.Remove(Guid.NewGuid()), Is.False);
    }

    [Test]
    public void Clear_RemovesAllRooms()
    {
        var store = new RoomComparisonStore();
        store.TryAdd(CreateCandidate("A"));
        store.TryAdd(CreateCandidate("B"));

        store.Clear();

        Assert.That(store.Rooms, Is.Empty);
    }

    [Test]
    public void Rooms_ReturnsSnapshot_ThatCannotMutateStore()
    {
        var store = new RoomComparisonStore();
        store.TryAdd(CreateCandidate("A"));

        var snapshot = store.Rooms;
        store.TryAdd(CreateCandidate("B"));

        Assert.That(snapshot, Has.Count.EqualTo(1));
    }

    private static ComparisonCandidate CreateCandidate(string title)
        => new(Guid.NewGuid(), title, null, 3_000_000, 25);
}
