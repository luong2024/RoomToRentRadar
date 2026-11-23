using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Tests;

public class RoomHouseRulesTests
{
    [Test]
    public void HouseRulesPolicy_Create_WithValidFields_ShouldTrimAndNormalize()
    {
        var policy = HouseRulesPolicy.Create(
            "  22:00 - 06:00  ",
            "  No overnight guests without notice  ",
            "  Shared kitchen until 21:30  ",
            "  1 motorbike slot per tenant  ",
            ["  No pets  ", "   ", "Keep hallway clean"]);

        Assert.Multiple(() =>
        {
            Assert.That(policy.QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(policy.GuestPolicy, Is.EqualTo("No overnight guests without notice"));
            Assert.That(policy.CookingRules, Is.EqualTo("Shared kitchen until 21:30"));
            Assert.That(policy.ParkingRules, Is.EqualTo("1 motorbike slot per tenant"));
            Assert.That(policy.AdditionalRules, Is.EqualTo(new[] { "No pets", "Keep hallway clean" }));
        });
    }

    [TestCase("", "Guests ok", "Cooking ok", "Parking ok")]
    [TestCase("22:00 - 06:00", " ", "Cooking ok", "Parking ok")]
    [TestCase("22:00 - 06:00", "Guests ok", "", "Parking ok")]
    [TestCase("22:00 - 06:00", "Guests ok", "Cooking ok", null)]
    public void HouseRulesPolicy_Create_WithMissingRequiredSection_ShouldThrowValidationException(
        string? quietHours,
        string? guestPolicy,
        string? cookingRules,
        string? parkingRules)
    {
        Assert.Throws<HouseRulesValidationException>(() =>
            HouseRulesPolicy.Create(quietHours, guestPolicy, cookingRules, parkingRules));
    }

    [Test]
    public void RoomHouseRules_CreateAndUpdate_ByOwnerLandlord_ShouldIncrementVersion()
    {
        var landlordId = Guid.NewGuid();
        var utc1 = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        var utc2 = utc1.AddHours(2);

        var policy1 = HouseRulesPolicy.Create("22:00 - 06:00", "Until 22:00", "Allowed", "Free 1 bike");
        var (rules, rev1) = RoomHouseRules.CreateInitial("room-1", landlordId, policy1, landlordId, isAdmin: false, "Initial", utc1);

        var policy2 = HouseRulesPolicy.Create("23:00 - 06:00", "Until 22:30", "Allowed", "Free 1 bike");
        var rev2 = rules.UpdatePolicy(policy2, landlordId, isAdmin: false, "Extended quiet hours", utc2);

        Assert.Multiple(() =>
        {
            Assert.That(rules.CurrentVersion, Is.EqualTo(2));
            Assert.That(rev1.Version, Is.EqualTo(1));
            Assert.That(rev2.Version, Is.EqualTo(2));
            Assert.That(rev2.ChangeNote, Is.EqualTo("Extended quiet hours"));
            Assert.That(rules.CurrentPolicy.QuietHours, Is.EqualTo("23:00 - 06:00"));
        });
    }

    [Test]
    public void RoomHouseRules_UpdateByDifferentLandlord_ShouldThrowOwnershipException()
    {
        var ownerId = Guid.NewGuid();
        var otherLandlordId = Guid.NewGuid();
        var policy = HouseRulesPolicy.Create("22:00 - 06:00", "Until 22:00", "Allowed", "Free 1 bike");
        var (rules, _) = RoomHouseRules.CreateInitial("room-1", ownerId, policy, ownerId, isAdmin: false, null, DateTimeOffset.UtcNow);

        Assert.Throws<HouseRulesOwnershipException>(() =>
            rules.UpdatePolicy(policy, otherLandlordId, isAdmin: false, "Hijack", DateTimeOffset.UtcNow));
    }

    [Test]
    public void HouseRulesPolicy_HasSameRules_ShouldCompareAllSectionsAndAdditionalRules()
    {
        var p1 = HouseRulesPolicy.Create("22:00", "Guests ok", "Cooking ok", "Parking ok", ["No pets"]);
        var p2 = HouseRulesPolicy.Create(" 22:00 ", "Guests ok", "Cooking ok", "Parking ok", ["  No pets  "]);
        var p3 = HouseRulesPolicy.Create("23:00", "Guests ok", "Cooking ok", "Parking ok", ["No pets"]);

        Assert.Multiple(() =>
        {
            Assert.That(p1.HasSameRules(p2), Is.True);
            Assert.That(p1.HasSameRules(p3), Is.False);
            Assert.That(p1.HasSameRules(null), Is.False);
        });
    }
}
