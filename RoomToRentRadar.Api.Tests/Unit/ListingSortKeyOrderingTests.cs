using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ListingSortKeyOrderingTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);

    private static readonly ListingSortKey[] Keys =
    [
        new(Id(0), Day3, 5_000_000, 30),
        new(Id(1), Day1, 7_000_000, 60),
        new(Id(2), Day3, 5_000_000, 60),
        new(Id(3), Day2, 7_000_000, 30),
        new(Id(4), Day3, 9_000_000, 45),
        new(Id(5), Day2, 5_000_000, 30)
    ];

    [TestCase(ListingSort.Newest, new[] { 0, 2, 4, 3, 5, 1 })]
    [TestCase(ListingSort.PriceAsc, new[] { 0, 2, 5, 1, 3, 4 })]
    [TestCase(ListingSort.PriceDesc, new[] { 4, 1, 3, 0, 2, 5 })]
    [TestCase(ListingSort.AreaDesc, new[] { 1, 2, 4, 0, 3, 5 })]
    public void Sort_MatchesExpectedOrdering(ListingSort sort, int[] expectedIndexes)
    {
        var ordered = ListingSortKeyOrdering.Sort(Keys, sort);

        Assert.That(ordered.Select(key => key.Id), Is.EqualTo(expectedIndexes.Select(Id)));
    }

    [Test]
    public void Sort_IsStableForDuplicateKeys_AndBreaksTiesByIdAscending()
    {
        // Rows 0, 2, 4 share Day3: order must be id ascending regardless of input order.
        var shuffled = new[] { Keys[4], Keys[2], Keys[0] };

        var ordered = ListingSortKeyOrdering.Sort(shuffled, ListingSort.Newest);

        Assert.That(ordered.Select(key => key.Id), Is.EqualTo(new[] { Id(0), Id(2), Id(4) }));
    }

    [TestCase(ListingSort.Newest, 1)]
    [TestCase(ListingSort.Newest, 2)]
    [TestCase(ListingSort.Newest, 3)]
    [TestCase(ListingSort.PriceAsc, 1)]
    [TestCase(ListingSort.PriceAsc, 4)]
    [TestCase(ListingSort.PriceAsc, 6)]
    [TestCase(ListingSort.PriceDesc, 2)]
    [TestCase(ListingSort.PriceDesc, 5)]
    [TestCase(ListingSort.AreaDesc, 1)]
    [TestCase(ListingSort.AreaDesc, 2)]
    [TestCase(ListingSort.AreaDesc, 6)]
    public void CursorPageWalk_ReturnsEveryRowExactlyOnce(ListingSort sort, int pageSize)
    {
        var expected = ListingSortKeyOrdering.Sort(Keys, sort).Select(key => key.Id).ToList();

        var collected = Walk(sort, pageSize);

        Assert.Multiple(() =>
        {
            Assert.That(collected, Is.EqualTo(expected));
            Assert.That(collected.Distinct().Count(), Is.EqualTo(collected.Count));
            Assert.That(collected, Is.EquivalentTo(expected));
        });
    }

    [TestCase(ListingSort.Newest)]
    [TestCase(ListingSort.PriceAsc)]
    [TestCase(ListingSort.PriceDesc)]
    [TestCase(ListingSort.AreaDesc)]
    public void IsAfterCursor_MatchesPositionInOrderedSequence(ListingSort sort)
    {
        var ordered = ListingSortKeyOrdering.Sort(Keys, sort);

        for (var cursorIndex = 0; cursorIndex < ordered.Count; cursorIndex++)
        {
            var last = ordered[cursorIndex];
            var cursor = Decode(ListingSortKeyOrdering.EncodeCursor(sort, last));

            var afterCursor = ordered
                .Where(key => ListingSortKeyOrdering.IsAfterCursor(key, cursor))
                .Select(key => key.Id)
                .ToList();

            Assert.That(
                afterCursor,
                Is.EqualTo(ordered.Skip(cursorIndex + 1).Select(key => key.Id)),
                $"Cursor predicate diverges at index {cursorIndex} for {sort}.");
        }
    }

    [Test]
    public void EncodeCursor_RoundTripsThroughTheSharedCodec()
    {
        var key = Keys[3];

        var encoded = ListingSortKeyOrdering.EncodeCursor(ListingSort.PriceDesc, key);
        Assert.That(ListingCursorCodec.TryDecode(encoded, out var cursor), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(cursor.Sort, Is.EqualTo(ListingSort.PriceDesc));
            Assert.That(cursor.Id, Is.EqualTo(key.Id));
            Assert.That(cursor.Price, Is.EqualTo(key.Price));
            Assert.That(cursor.CreatedAt, Is.Null);
            Assert.That(cursor.AreaM2, Is.Null);
        });
    }

    [Test]
    public void NewestCursor_RoundTripsCreatedAtAsUtc()
    {
        var key = Keys[0];

        var encoded = ListingSortKeyOrdering.EncodeCursor(ListingSort.Newest, key);
        Assert.That(ListingCursorCodec.TryDecode(encoded, out var cursor), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(cursor.CreatedAt, Is.EqualTo(Day3));
            Assert.That(cursor.CreatedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Comparer_RejectsUnknownSort()
    {
        Assert.That(
            () => ListingSortKeyOrdering.Comparer((ListingSort)99),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static List<Guid> Walk(ListingSort sort, int pageSize)
    {
        var ordered = ListingSortKeyOrdering.Sort(Keys, sort);
        var collected = new List<Guid>();
        ListingCursor? cursor = null;

        for (var page = 0; page < 50; page++)
        {
            var remaining = cursor is null
                ? ordered
                : ordered.Where(key => ListingSortKeyOrdering.IsAfterCursor(key, cursor)).ToList();

            var window = remaining.Take(pageSize + 1).ToList();
            var hasMore = window.Count > pageSize;
            if (hasMore)
            {
                window.RemoveAt(window.Count - 1);
            }

            collected.AddRange(window.Select(key => key.Id));

            if (!hasMore)
            {
                break;
            }

            cursor = Decode(ListingSortKeyOrdering.EncodeCursor(sort, window[^1]));
        }

        return collected;
    }

    private static ListingCursor Decode(string encoded)
    {
        Assert.That(ListingCursorCodec.TryDecode(encoded, out var cursor), Is.True);
        return cursor;
    }

    private static Guid Id(int index) => new($"00000000-0000-4000-8000-{index:D12}");
}
