using System.Buffers.Text;
using System.Text;
using RoomToRentRadar.Api.Features.Moderation;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ModerationQueueCursorCodecTests
{
    private static readonly Guid ListingId = Guid.Parse("a6d4a502-8172-f03b-991b-121d1b1ecfba");
    private static readonly DateTime OldestReportAt =
        new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc).AddTicks(7654321);

    [Test]
    public void Cursor_RoundTrips()
    {
        var encoded = ModerationQueueCursorCodec.Encode(3, OldestReportAt, ListingId);

        Assert.That(ModerationQueueCursorCodec.TryDecode(encoded, out var cursor), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(cursor.TotalReports, Is.EqualTo(3));
            Assert.That(cursor.OldestReportAt, Is.EqualTo(OldestReportAt));
            Assert.That(cursor.OldestReportAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(cursor.ListingId, Is.EqualTo(ListingId));
        });
    }

    [Test]
    public void Cursor_NonUtcInput_IsStoredAsUtc()
    {
        var local = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Local);

        var encoded = ModerationQueueCursorCodec.Encode(1, local, ListingId);

        Assert.That(ModerationQueueCursorCodec.TryDecode(encoded, out var cursor), Is.True);
        Assert.That(cursor.OldestReportAt, Is.EqualTo(local.ToUniversalTime()));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-a-cursor")]
    [TestCase("AAAA")]
    public void Cursor_InvalidValue_IsRejected(string? value)
    {
        Assert.That(ModerationQueueCursorCodec.TryDecode(value, out _), Is.False);
    }

    [Test]
    public void Cursor_UnknownVersion_IsRejected()
    {
        var encoded = EncodePayload(
            """{"version":99,"totalReports":2,"oldestReportAt":"2026-02-03T04:05:06.0000000Z","listingId":"a6d4a502-8172-f03b-991b-121d1b1ecfba"}""");

        Assert.That(ModerationQueueCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void Cursor_ZeroTotalReports_IsRejected()
    {
        var encoded = EncodePayload(
            """{"version":1,"totalReports":0,"oldestReportAt":"2026-02-03T04:05:06.0000000Z","listingId":"a6d4a502-8172-f03b-991b-121d1b1ecfba"}""");

        Assert.That(ModerationQueueCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void Cursor_EmptyListingId_IsRejected()
    {
        var encoded = EncodePayload(
            """{"version":1,"totalReports":2,"oldestReportAt":"2026-02-03T04:05:06.0000000Z","listingId":"00000000-0000-0000-0000-000000000000"}""");

        Assert.That(ModerationQueueCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    [Test]
    public void Cursor_InvalidTimestamp_IsRejected()
    {
        var encoded = EncodePayload(
            """{"version":1,"totalReports":2,"oldestReportAt":"yesterday","listingId":"a6d4a502-8172-f03b-991b-121d1b1ecfba"}""");

        Assert.That(ModerationQueueCursorCodec.TryDecode(encoded, out _), Is.False);
    }

    private static string EncodePayload(string json)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
}
