using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ListingQueryValidatorTests
{
    [Test]
    public void Defaults_AreApplied()
    {
        var parameters = new ListingQueryParameters();

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out var problem), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Null);
            Assert.That(query.Limit, Is.EqualTo(ListingQueryValidator.DefaultLimit));
            Assert.That(query.Sort, Is.EqualTo(ListingSort.Newest));
            Assert.That(query.Cursor, Is.Null);
            Assert.That(query.MinPrice, Is.Null);
            Assert.That(query.MaxPrice, Is.Null);
            Assert.That(query.Bedrooms, Is.Null);
        });
    }

    [Test]
    public void AllValidValues_AreMapped()
    {
        var parameters = new ListingQueryParameters
        {
            Limit = 50,
            Sort = ListingSortWire.AreaDesc,
            Cursor = "abc",
            MinPrice = 1_000_000,
            MaxPrice = 5_000_000,
            Bedrooms = 2
        };

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out _), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(query.Limit, Is.EqualTo(50));
            Assert.That(query.Sort, Is.EqualTo(ListingSort.AreaDesc));
            Assert.That(query.Cursor, Is.EqualTo("abc"));
            Assert.That(query.MinPrice, Is.EqualTo(1_000_000));
            Assert.That(query.MaxPrice, Is.EqualTo(5_000_000));
            Assert.That(query.Bedrooms, Is.EqualTo(2));
        });
    }

    [TestCase(0, "invalid_limit")]
    [TestCase(-1, "invalid_limit")]
    [TestCase(101, "invalid_limit")]
    public void InvalidLimit_IsRejected(int limit, string expectedCode)
        => AssertRejected(new ListingQueryParameters { Limit = limit }, expectedCode);

    [TestCase("popular", "invalid_sort")]
    [TestCase("PriceAsc", "invalid_sort")]
    [TestCase("", "invalid_sort")]
    public void InvalidSort_IsRejected(string sort, string expectedCode)
        => AssertRejected(new ListingQueryParameters { Sort = sort }, expectedCode);

    [Test]
    public void NegativeMinPrice_IsRejected()
        => AssertRejected(new ListingQueryParameters { MinPrice = -1 }, "invalid_price_range");

    [Test]
    public void MinPriceGreaterThanMaxPrice_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { MinPrice = 10, MaxPrice = 5 },
            "invalid_price_range");

    [TestCase(-1)]
    [TestCase(21)]
    public void InvalidBedrooms_IsRejected(int bedrooms)
        => AssertRejected(new ListingQueryParameters { Bedrooms = bedrooms }, "invalid_bedrooms");

    [Test]
    public void WhitespaceCursor_IsTreatedAsMissing()
    {
        var parameters = new ListingQueryParameters { Cursor = "   " };

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out _), Is.True);
        Assert.That(query.Cursor, Is.Null);
    }

    private static void AssertRejected(ListingQueryParameters parameters, string expectedCode)
    {
        Assert.That(ListingQueryValidator.TryCreate(parameters, out _, out var problem), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(400));
            Assert.That(problem.Extensions["code"], Is.EqualTo(expectedCode));
        });
    }
}
