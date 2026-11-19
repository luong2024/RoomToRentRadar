using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingFormValidatorTests
{
    [Test]
    public void Validate_EmptyForm_ReportsEveryRequiredField()
    {
        var fields = new ListingFormFields(" ", "", "", "", "", null, "", "", "");

        var errors = ListingFormValidator.Validate(fields, 0);

        Assert.That(errors.Keys, Is.EquivalentTo(new[]
            { "title", "address", "price", "deposit", "area_m2", "room_type", "lat", "lng", "photos" }));
    }

    [Test]
    public void Validate_CompleteFormWithZeroDepositAndThreePhotos_IsValid()
    {
        var errors = ListingFormValidator.Validate(ValidFields(), 3);

        Assert.That(errors, Is.Empty);
    }

    [TestCase(0, false)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(20, true)]
    [TestCase(21, false)]
    public void Validate_PhotoCount_EnforcesMinimumAndMaximum(int count, bool expectedValid)
    {
        var errors = ListingFormValidator.Validate(ValidFields(), count);

        Assert.That(errors.ContainsKey("photos"), Is.EqualTo(!expectedValid));
    }

    [TestCase("", "0", "20", "price")]
    [TestCase("0", "0", "20", "price")]
    [TestCase("-1", "0", "20", "price")]
    [TestCase("1.5", "0", "20", "price")]
    [TestCase("2500000", "", "20", "deposit")]
    [TestCase("2500000", "-1", "20", "deposit")]
    [TestCase("2500000", "0", "0", "area_m2")]
    [TestCase("2500000", "0", "NaN", "area_m2")]
    [TestCase("2500000", "0", "Infinity", "area_m2")]
    public void Validate_InvalidNumericField_ReportsItsError(string rent, string deposit, string area, string expectedKey)
    {
        var errors = ListingFormValidator.Validate(ValidFields() with { Rent = rent, Deposit = deposit, Area = area }, 3);

        Assert.That(errors.ContainsKey(expectedKey), Is.True);
    }

    [TestCase("20.5")]
    [TestCase("20,5")]
    public void Validate_DecimalArea_AcceptsDotAndComma(string area)
    {
        var errors = ListingFormValidator.Validate(ValidFields() with { Area = area }, 3);

        Assert.That(errors, Is.Empty);
    }

    [TestCase("91", "106.7", "lat")]
    [TestCase("-90.01", "106.7", "lat")]
    [TestCase("10.75", "181", "lng")]
    [TestCase("10.75", "-180.01", "lng")]
    public void Validate_OutOfRangeCoordinates_ReportsLocationError(string latitude, string longitude, string expectedKey)
    {
        var errors = ListingFormValidator.Validate(ValidFields() with { Latitude = latitude, Longitude = longitude }, 3);

        Assert.That(errors.ContainsKey(expectedKey), Is.True);
    }

    [Test]
    public void Validate_UnknownRoomType_ReportsSelectionError()
    {
        var errors = ListingFormValidator.Validate(ValidFields() with { RoomType = (RoomType)999 }, 3);

        Assert.That(errors.ContainsKey("room_type"), Is.True);
    }

    [Test]
    public void Validate_TextOverServerLimits_ReportsFieldErrors()
    {
        var errors = ListingFormValidator.Validate(ValidFields() with
        {
            Title = new string('a', 201), Address = new string('b', 301), Description = new string('c', 4001)
        }, 3);

        Assert.That(errors.Keys, Is.EquivalentTo(new[] { "title", "address", "description" }));
    }

    private static ListingFormFields ValidFields()
        => new("Room near campus", "10 Campus Road", "2500000", "0", "20.5", RoomType.Studio, "10.75", "106.7", "");
}
