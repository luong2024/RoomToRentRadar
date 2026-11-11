using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class RoomTests
{
    [Test]
    public void NewRoom_HasSafeDefaults()
    {
        var room = new Room();

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.Not.Empty.And.Length.EqualTo(32));
            Assert.That(room.Title, Is.EqualTo(string.Empty));
            Assert.That(room.Description, Is.EqualTo(string.Empty));
            Assert.That(room.Address, Is.EqualTo(string.Empty));
            Assert.That(room.LandlordId, Is.EqualTo(string.Empty));
            Assert.That(room.IsAvailable, Is.True);
            Assert.That(room.IsVerified, Is.True);
            Assert.That(room.Price, Is.EqualTo(0m));
            Assert.That(room.Area, Is.EqualTo(0.0));
        });
    }

    [Test]
    public void Room_WithInitProperties_StoresValues()
    {
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var room = new Room
        {
            Id = "room-1",
            Title = "Phòng trọ Sinh viên Z115",
            Description = "Phòng sạch sẽ",
            Price = 1_500_000m,
            Area = 18.0,
            Address = "Z115, Thái Nguyên",
            Latitude = 21.5875,
            Longitude = 105.8080,
            LandlordId = "landlord-1",
            IsAvailable = false,
            IsVerified = false,
            CreatedAtUtc = createdAt
        };

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.EqualTo("room-1"));
            Assert.That(room.Title, Is.EqualTo("Phòng trọ Sinh viên Z115"));
            Assert.That(room.Price, Is.EqualTo(1_500_000m));
            Assert.That(room.Area, Is.EqualTo(18.0));
            Assert.That(room.Latitude, Is.EqualTo(21.5875));
            Assert.That(room.Longitude, Is.EqualTo(105.8080));
            Assert.That(room.IsAvailable, Is.False);
            Assert.That(room.IsVerified, Is.False);
            Assert.That(room.CreatedAtUtc, Is.EqualTo(createdAt));
        });
    }
}
