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
            Assert.That(query.MinAreaM2, Is.Null);
            Assert.That(query.MaxAreaM2, Is.Null);
            Assert.That(query.RoomType, Is.Null);
            Assert.That(query.Campus, Is.Null);
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
            Bedrooms = 2,
            MinAreaM2 = 25.5,
            MaxAreaM2 = 80d,
            RoomType = RoomTypeWire.Studio,
            CampusLat = 10.7626,
            CampusLng = 106.6602,
            MaxDistanceKm = 3.5
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
            Assert.That(query.MinAreaM2, Is.EqualTo(25.5));
            Assert.That(query.MaxAreaM2, Is.EqualTo(80d));
            Assert.That(query.RoomType, Is.EqualTo(RoomType.Studio));
            Assert.That(query.Campus, Is.EqualTo(new CampusFilter(10.7626, 106.6602, 3.5)));
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
    public void NegativeMinArea_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { MinAreaM2 = -0.5 },
            ListingProblemFactory.InvalidAreaRange);

    [Test]
    public void NegativeMaxArea_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { MaxAreaM2 = -10 },
            ListingProblemFactory.InvalidAreaRange);

    [Test]
    public void MinAreaGreaterThanMaxArea_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { MinAreaM2 = 60, MaxAreaM2 = 50 },
            ListingProblemFactory.InvalidAreaRange);

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void NonFiniteArea_IsRejected(double area)
        => AssertRejected(
            new ListingQueryParameters { MaxAreaM2 = area },
            ListingProblemFactory.InvalidAreaRange);

    [Test]
    public void EqualAreaBounds_AreAccepted()
    {
        var parameters = new ListingQueryParameters { MinAreaM2 = 30, MaxAreaM2 = 30 };

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out _), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(query.MinAreaM2, Is.EqualTo(30d));
            Assert.That(query.MaxAreaM2, Is.EqualTo(30d));
        });
    }

    [TestCase("single_room", RoomType.SingleRoom)]
    [TestCase("shared_room", RoomType.SharedRoom)]
    [TestCase("studio", RoomType.Studio)]
    [TestCase("apartment", RoomType.Apartment)]
    [TestCase("house", RoomType.House)]
    public void ValidRoomType_IsMapped(string wireValue, RoomType expected)
    {
        var parameters = new ListingQueryParameters { RoomType = wireValue };

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out _), Is.True);
        Assert.That(query.RoomType, Is.EqualTo(expected));
    }

    [TestCase("castle")]
    [TestCase("Studio")]
    [TestCase("")]
    public void InvalidRoomType_IsRejected(string roomType)
        => AssertRejected(
            new ListingQueryParameters { RoomType = roomType },
            ListingProblemFactory.InvalidRoomType);

    [Test]
    public void CampusLatitudeWithoutOtherMembers_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { CampusLat = 10.7 },
            ListingProblemFactory.InvalidCampusFilter);

    [Test]
    public void CampusCoordinatesWithoutDistance_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { CampusLat = 10.7, CampusLng = 106.7 },
            ListingProblemFactory.InvalidCampusFilter);

    [Test]
    public void CampusDistanceWithoutCentre_IsRejected()
        => AssertRejected(
            new ListingQueryParameters { MaxDistanceKm = 5 },
            ListingProblemFactory.InvalidCampusFilter);

    [TestCase(90.5, 106.7)]
    [TestCase(-90.5, 106.7)]
    [TestCase(10.7, 180.5)]
    [TestCase(10.7, -180.5)]
    [TestCase(double.NaN, 106.7)]
    [TestCase(10.7, double.PositiveInfinity)]
    public void OutOfRangeCampusCoordinates_AreRejected(double lat, double lng)
        => AssertRejected(
            new ListingQueryParameters { CampusLat = lat, CampusLng = lng, MaxDistanceKm = 5 },
            ListingProblemFactory.InvalidCampusCoordinates);

    [TestCase(0d)]
    [TestCase(-1d)]
    [TestCase(50.5)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void OutOfRangeDistance_IsRejected(double maxDistanceKm)
        => AssertRejected(
            new ListingQueryParameters { CampusLat = 10.7, CampusLng = 106.7, MaxDistanceKm = maxDistanceKm },
            ListingProblemFactory.InvalidDistanceRange);

    [Test]
    public void MaximumDistance_IsAccepted()
    {
        var parameters = new ListingQueryParameters
        {
            CampusLat = 10.7,
            CampusLng = 106.7,
            MaxDistanceKm = ListingQueryValidator.MaxDistanceKm
        };

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out _), Is.True);
        Assert.That(query.Campus!.MaxDistanceKm, Is.EqualTo(ListingQueryValidator.MaxDistanceKm));
    }

    [TestCase(90d, 180d)]
    [TestCase(-90d, -180d)]
    public void ExtremeCampusCoordinates_AreAccepted(double lat, double lng)
    {
        var parameters = new ListingQueryParameters { CampusLat = lat, CampusLng = lng, MaxDistanceKm = 1 };

        Assert.That(ListingQueryValidator.TryCreate(parameters, out var query, out _), Is.True);
        Assert.That(query.Campus, Is.EqualTo(new CampusFilter(lat, lng, 1d)));
    }

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
