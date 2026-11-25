using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Profile;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class InMemoryProfileDetailsStoreTests
{
    private static readonly Guid UserId = Guid.Parse("0c7a1f42-3b5e-4a8d-9c61-7d2f90a1b2c3");

    [Test]
    public void GetStudentDetails_SeedsFromFallbackNameAndRemainsStable()
    {
        var store = new InMemoryProfileDetailsStore();

        var first = store.GetStudentDetails(UserId, "Minh Anh");
        var second = store.GetStudentDetails(UserId, "Tên Khác");

        Assert.Multiple(() =>
        {
            Assert.That(first.FullName, Is.EqualTo("Minh Anh"));
            Assert.That(first.University, Is.EqualTo("Đại học Bách khoa TP.HCM"));
            Assert.That(first.DateOfBirth, Is.EqualTo(new DateTime(2003, 5, 12)));
            Assert.That(first.MoveInDate, Is.EqualTo(DateTime.Today.AddDays(30)));
            Assert.That(second, Is.SameAs(first));
        });
    }

    [Test]
    public void GetStudentDetails_WithBlankName_UsesFallbackLabel()
    {
        var store = new InMemoryProfileDetailsStore();

        var details = store.GetStudentDetails(UserId, "   ");

        Assert.That(details.FullName, Is.EqualTo("Người dùng"));
    }

    [Test]
    public void SaveStudentDetails_RoundTripsValues()
    {
        var store = new InMemoryProfileDetailsStore();
        var details = new StudentProfileDetails
        {
            FullName = "Tên Mới",
            AvatarPath = "/data/avatar.jpg",
            Phone = "0912345678",
            University = "Đại học Quốc gia",
            HasPets = true
        };

        store.SaveStudentDetails(UserId, details);

        var saved = store.GetStudentDetails(UserId, "Fallback");
        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.SameAs(details));
            Assert.That(saved.FullName, Is.EqualTo("Tên Mới"));
            Assert.That(saved.AvatarPath, Is.EqualTo("/data/avatar.jpg"));
            Assert.That(saved.Phone, Is.EqualTo("0912345678"));
            Assert.That(saved.HasPets, Is.True);
        });
    }

    [Test]
    public void GetLandlordDetails_SeedsVerificationStatsAndReputation()
    {
        var store = new InMemoryProfileDetailsStore();

        var details = store.GetLandlordDetails(UserId, "Nhà trọ Bình An");

        Assert.Multiple(() =>
        {
            Assert.That(details.FullName, Is.EqualTo("Nhà trọ Bình An"));
            Assert.That(details.Phone, Is.EqualTo("0987 654 321"));
            Assert.That(details.AccountType, Is.EqualTo("Cá nhân"));
            Assert.That(details.BusinessName, Is.EqualTo("Nhà trọ Bình An"));
            Assert.That(details.PhoneVerification, Is.EqualTo(ProfileVerificationStatus.Verified));
            Assert.That(details.EmailVerification, Is.EqualTo(ProfileVerificationStatus.Verified));
            Assert.That(details.IdentityVerification, Is.EqualTo(ProfileVerificationStatus.Pending));
            Assert.That(details.PropertyOwnershipVerification, Is.EqualTo(ProfileVerificationStatus.Unverified));
            Assert.That(details.RoomStats.Total, Is.EqualTo(6));
            Assert.That(details.RoomStats.Available, Is.EqualTo(3));
            Assert.That(details.RoomStats.Occupied, Is.EqualTo(2));
            Assert.That(details.RoomStats.Pending, Is.EqualTo(1));
            Assert.That(details.Reputation.AverageRating, Is.EqualTo(4.6));
            Assert.That(details.Reputation.TotalReviews, Is.EqualTo(28));
            Assert.That(details.Reputation.ResponseRatePercent, Is.EqualTo(92));
        });
    }

    [Test]
    public void SaveLandlordDetails_RoundTripsValues()
    {
        var store = new InMemoryProfileDetailsStore();
        var details = new LandlordProfileDetails
        {
            FullName = "Chủ Trọ",
            BusinessName = "Nhà trọ Mới",
            PhoneVerification = ProfileVerificationStatus.Verified
        };

        store.SaveLandlordDetails(UserId, details);

        var saved = store.GetLandlordDetails(UserId, "Fallback");
        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.SameAs(details));
            Assert.That(saved.BusinessName, Is.EqualTo("Nhà trọ Mới"));
            Assert.That(saved.PhoneVerification, Is.EqualTo(ProfileVerificationStatus.Verified));
        });
    }

    [Test]
    public void GetSettings_SeedsDefaultsAndSavesPerUser()
    {
        var store = new InMemoryProfileDetailsStore();
        var otherUserId = Guid.NewGuid();

        var defaults = store.GetSettings(UserId);
        store.SaveSettings(UserId, new UserSettings
        {
            NotificationsEnabled = false,
            NewListingsEnabled = false,
            ShowPhoneEnabled = true,
            AllowMessagesEnabled = false
        });

        var saved = store.GetSettings(UserId);
        var other = store.GetSettings(otherUserId);
        Assert.Multiple(() =>
        {
            Assert.That(defaults.NotificationsEnabled, Is.True);
            Assert.That(defaults.NewListingsEnabled, Is.True);
            Assert.That(defaults.ShowPhoneEnabled, Is.False);
            Assert.That(defaults.AllowMessagesEnabled, Is.True);
            Assert.That(saved.NotificationsEnabled, Is.False);
            Assert.That(saved.ShowPhoneEnabled, Is.True);
            Assert.That(other.NotificationsEnabled, Is.True);
            Assert.That(other.ShowPhoneEnabled, Is.False);
        });
    }
}
