using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Domain.Tests;

[TestFixture]
public class FavouriteRoomTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void Create_WhenValidInputs_ShouldPreserveCanonicalRoomIdAndBuildNormalizedDeterministicId()
    {
        var userId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        const string canonicalRoomId = "  Room-ICTU-001A  ";

        var favourite = FavouriteRoom.Create(userId, canonicalRoomId, FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(favourite.UserId, Is.EqualTo(userId));
            Assert.That(favourite.RoomId, Is.EqualTo("Room-ICTU-001A"));
            Assert.That(favourite.NormalizedRoomId, Is.EqualTo("room-ictu-001a"));
            Assert.That(favourite.Id, Is.EqualTo("3fa85f64-5717-4562-b3fc-2c963f66afa6:room-ictu-001a"));
            Assert.That(favourite.CreatedAtUtc, Is.EqualTo(FixedNow));
        });
    }

    [Test]
    public void Create_WhenUserIdIsEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FavouriteRoom.Create(Guid.Empty, "room-ictu-001", FixedNow));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_WhenRoomIdIsNullOrWhitespace_ShouldThrowArgumentException(string? invalidRoomId)
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            FavouriteRoom.Create(userId, invalidRoomId!, FixedNow));
    }

    [Test]
    public void Create_WhenRoomIdLengthIsAtMaxBoundary128_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var maxRoomId = new string('a', FavouriteRoom.MaxRoomIdLength);

        var favourite = FavouriteRoom.Create(userId, maxRoomId, FixedNow);

        Assert.That(favourite.RoomId, Has.Length.EqualTo(FavouriteRoom.MaxRoomIdLength));
    }

    [Test]
    public void Create_WhenRoomIdExceedsMaxLength128_ShouldThrowArgumentException()
    {
        var userId = Guid.NewGuid();
        var tooLongRoomId = new string('a', FavouriteRoom.MaxRoomIdLength + 1);

        var ex = Assert.Throws<ArgumentException>(() =>
            FavouriteRoom.Create(userId, tooLongRoomId, FixedNow));

        Assert.That(ex!.Message, Does.Contain("128"));
    }

    [TestCase("room/001")]
    [TestCase("room\\001")]
    [TestCase("room?001")]
    [TestCase("room#001")]
    [TestCase("room\u0000001")]
    [TestCase("room\n001")]
    [TestCase("room\t001")]
    public void TryValidateRoomId_WhenContainingForbiddenCosmosOrControlCharacters_ShouldReturnFalse(string invalidRoomId)
    {
        var isValid = FavouriteRoom.TryValidateRoomId(invalidRoomId, out _, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void BuildId_ShouldProduceSameIdRegardlessOfRoomIdCasingOrOuterWhitespace()
    {
        var userId = Guid.NewGuid();

        var id1 = FavouriteRoom.BuildId(userId, "Room-ICTU-001");
        var id2 = FavouriteRoom.BuildId(userId, "  room-ictu-001 ");

        Assert.That(id1, Is.EqualTo(id2));
    }
}
