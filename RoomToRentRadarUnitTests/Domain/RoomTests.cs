using System.Reflection;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class RoomTests
{
    private static Room CreateValidRoom() => Room.Create(
        title: "Phòng trọ Sinh viên Z115",
        description: "Phòng sạch sẽ",
        price: 1_500_000m,
        area: 18.0,
        address: "Z115, Thái Nguyên",
        latitude: 21.5875,
        longitude: 105.8080,
        landlordId: "landlord-1");

    [Test]
    public void Room_HasNoPublicParameterlessConstructor()
    {
        var publicParameterlessCtors = typeof(Room)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length == 0)
            .ToList();

        Assert.That(publicParameterlessCtors, Is.Empty,
            "Room phải được khởi tạo qua Room.Create hoặc Room.Reconstitute (Guardrail 1.5.3).");
    }

    [Test]
    public void Create_WithValidData_GeneratesIdAndDefaults()
    {
        var before = DateTime.UtcNow;

        var room = CreateValidRoom();

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.Not.Empty.And.Length.EqualTo(32));
            Assert.That(room.IsAvailable, Is.True);
            Assert.That(room.IsVerified, Is.True);
            Assert.That(room.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(room.CreatedAtUtc, Is.InRange(before, DateTime.UtcNow));
        });
    }

    [Test]
    public void Create_WithEmptyTitleOrLandlord_ThrowsDomainValidationException()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => Room.Create(
                title: " ", description: "d", price: 1_000_000m, area: 10, address: "a",
                latitude: 21.5, longitude: 105.8, landlordId: "landlord-1"));
            Assert.Throws<DomainValidationException>(() => Room.Create(
                title: "Phòng", description: "d", price: 1_000_000m, area: 10, address: "a",
                latitude: 21.5, longitude: 105.8, landlordId: ""));
        });
    }

    [Test]
    public void Create_WithNonPositivePriceOrArea_ThrowsDomainValidationException()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => Room.Create(
                title: "Phòng", description: "d", price: 0m, area: 10, address: "a",
                latitude: 21.5, longitude: 105.8, landlordId: "landlord-1"));
            Assert.Throws<DomainValidationException>(() => Room.Create(
                title: "Phòng", description: "d", price: 1_000_000m, area: 0, address: "a",
                latitude: 21.5, longitude: 105.8, landlordId: "landlord-1"));
        });
    }

    [Test]
    public void Create_WithOutOfRangeCoordinates_ThrowsDomainValidationException()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => Room.Create(
                title: "Phòng", description: "d", price: 1_000_000m, area: 10, address: "a",
                latitude: 91, longitude: 105.8, landlordId: "landlord-1"));
            Assert.Throws<DomainValidationException>(() => Room.Create(
                title: "Phòng", description: "d", price: 1_000_000m, area: 10, address: "a",
                latitude: 21.5, longitude: -181, landlordId: "landlord-1"));
        });
    }

    [Test]
    public void Reconstitute_StoresAllValues()
    {
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var room = Room.Reconstitute(
            id: "room-1",
            title: "Phòng trọ Sinh viên Z115",
            description: "Phòng sạch sẽ",
            price: 1_500_000m,
            area: 18.0,
            address: "Z115, Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            landlordId: "landlord-1",
            isAvailable: false,
            isVerified: false,
            createdAtUtc: createdAt);

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
