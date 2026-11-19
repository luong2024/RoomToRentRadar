using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class HouseRulesValidatorTests
{
    [Test]
    public void TryValidate_WhenAllFieldsPresent_TrimsAndSucceeds()
    {
        var request = new UpdateHouseRulesRequest
        {
            QuietHours = "  22:00 - 06:00  ",
            GuestPolicy = " Guests leave before 22:00 ",
            CookingRules = " Cook in the shared kitchen ",
            ParkingRules = " Park on the ground floor "
        };

        var valid = HouseRulesValidator.TryValidate(request, out var input, out var errors);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(input.QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(input.GuestPolicy, Is.EqualTo("Guests leave before 22:00"));
            Assert.That(input.CookingRules, Is.EqualTo("Cook in the shared kitchen"));
            Assert.That(input.ParkingRules, Is.EqualTo("Park on the ground floor"));
        });
    }

    [Test]
    public void TryValidate_WhenAllFieldsMissing_ReportsEveryRequiredField()
    {
        var valid = HouseRulesValidator.TryValidate(
            new UpdateHouseRulesRequest(),
            out _,
            out var errors);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(
                errors.Keys,
                Is.EquivalentTo(new[]
                {
                    "quiet_hours",
                    "guest_policy",
                    "cooking_rules",
                    "parking_rules"
                }));
            Assert.That(
                errors.Values.SelectMany(messages => messages),
                Has.All.EqualTo("This field is required."));
        });
    }

    [TestCase(HouseRulesValidator.MaxFieldLength, true)]
    [TestCase(HouseRulesValidator.MaxFieldLength + 1, false)]
    public void TryValidate_FieldLengthBoundary(int length, bool expectedValid)
    {
        var request = CreateRequest(parkingRules: new string('a', length));

        var valid = HouseRulesValidator.TryValidate(request, out _, out var errors);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.EqualTo(expectedValid));
            Assert.That(errors.ContainsKey("parking_rules"), Is.EqualTo(!expectedValid));
        });
    }

    private static UpdateHouseRulesRequest CreateRequest(string parkingRules) => new()
    {
        QuietHours = "22:00 - 06:00",
        GuestPolicy = "Guests leave before 22:00",
        CookingRules = "Cook in the shared kitchen",
        ParkingRules = parkingRules
    };
}
