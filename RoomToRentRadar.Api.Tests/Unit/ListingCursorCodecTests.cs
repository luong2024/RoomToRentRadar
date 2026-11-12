using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ListingCursorCodecTests
{
    private static readonly Guid ListingId = Guid.Parse("a6d4a502-8172-f03b-991b-121d1b1ecfba");
    private static readonly DateTime CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
    private const string Fingerprint = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Test]
    public void NewestCursor_RoundTrips()
    {
        var encoded = ListingCursorCodec.Encode(
            ListingSort.Newest, ListingId, Fingerprint, createdAt: CreatedAt);

        Assert.That(ListingCursorCodec.TryDecode(encoded, out var cursor), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(cursor.Sort, Is.EqualTo(ListingSort.Newest));
            Assert.That(cursor.Id, Is.EqualTo(ListingId));
            Assert.That(cursor.FilterFingerprint, Is.EqualTo(Fingerprint));
            Assert.That(cursor.CreatedAt, Is.EqualTo(CreatedAt));
            Assert.That(cursor.CreatedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(cursor.Price, Is.Null);
            Assert.That(cursor.AreaM2, Is.Null);
        });
    }

    [Test]
    public void PriceCursor_RoundTrips()
    {
        var encoded = ListingCursorCodec.Encode(
            ListingSort.PriceDesc, ListingId, Fingerprint, price: 12_345_678);

        Assert.That(ListingCursorCodec.TryDecode(encoded, out var cursor), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(cursor.Sort, Is.EqualTo(ListingSort.PriceDesc));
            Assert.That(cursor.Price, Is.EqualTo(12_345_678));
            Assert.That(cursor.CreatedAt, Is.Null);
            Assert.That(cursor.AreaM2, Is.Null);
        });
    }

    [Test]
    public void AreaCursor_RoundTrips()
    {
        var encoded = ListingCursorCodec.Encode(
            ListingSort.AreaDesc, ListingId, Fingerprint, areaM2: 42.5);

        Assert.That(ListingCursorCodec.TryDecode(encoded, out var cursor), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(cursor.Sort, Is.EqualTo(ListingSort.AreaDesc));
            Assert.That(cursor.AreaM2, Is.EqualTo(42.5));
            Assert.That(cursor.Price, Is.Null);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-a-cursor")]
    [TestCase("eyJ2ZXJzaW9uIjoxfQ")]
    public void MalformedCursor_IsRejected(string? value)
    {
        Assert.That(ListingCursorCodec.TryDecode(value, out _), Is.False);
    }

    [Test]
    public void UnsupportedVersion_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = 3,
            sort = "newest",
            key = CreatedAt.ToString("O"),
            id = ListingId,
            fingerprint = Fingerprint
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void VersionWithoutFingerprint_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = 1,
            sort = "newest",
            key = CreatedAt.ToString("O"),
            id = ListingId
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void BlankFingerprint_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = ListingCursorCodec.CurrentVersion,
            sort = "newest",
            key = CreatedAt.ToString("O"),
            id = ListingId,
            fingerprint = "   "
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void UnknownSort_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = ListingCursorCodec.CurrentVersion,
            sort = "popular",
            key = "1",
            id = ListingId,
            fingerprint = Fingerprint
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void InvalidNewestKey_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = ListingCursorCodec.CurrentVersion,
            sort = "newest",
            key = "not-a-date",
            id = ListingId,
            fingerprint = Fingerprint
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void InvalidPriceKey_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = ListingCursorCodec.CurrentVersion,
            sort = "price_asc",
            key = "not-a-price",
            id = ListingId,
            fingerprint = Fingerprint
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void EmptyId_IsRejected()
    {
        var encoded = EncodePayload(new
        {
            version = ListingCursorCodec.CurrentVersion,
            sort = "newest",
            key = CreatedAt.ToString("O"),
            id = Guid.Empty,
            fingerprint = Fingerprint
        });

        Assert.That(ListingCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    private static string EncodePayload(object payload)
        => Base64Url.EncodeToString(
            JsonSerializer.SerializeToUtf8Bytes(payload, RoomToRentRadar.Shared.Serialization.ApiJson.Options));
}
