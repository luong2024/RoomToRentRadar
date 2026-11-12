using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Tests;

public class UserProfileTests
{
    [Test]
    public void CreateDefault_ForStudent_ShouldInitializeTenantDetails()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var profile = UserProfile.CreateDefault(userId, UserRole.Student, now);

        Assert.Multiple(() =>
        {
            Assert.That(profile.UserId, Is.EqualTo(userId));
            Assert.That(profile.Role, Is.EqualTo(UserRole.Student));
            Assert.That(profile.Tenant, Is.Not.Null);
            Assert.That(profile.Tenant!.University, Is.EqualTo("ICTU"));
            Assert.That(profile.Landlord, Is.Null);
        });
    }

    [Test]
    public void UpdateTenantDetails_WhenCalledByLandlord_ShouldThrowProfileRoleMismatchException()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, DateTimeOffset.UtcNow);
        var tenantInfo = new TenantDetails("DTC123", "ICTU", "IT", null, Gender.Male, null, null, null);

        Assert.Throws<ProfileRoleMismatchException>(() =>
            profile.UpdateTenantDetails(tenantInfo, DateTimeOffset.UtcNow));
    }

    [Test]
    public void LandlordKyc_FullLifecycle_ShouldTransitionCorrectly()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, DateTimeOffset.UtcNow);
        Assert.That(profile.Landlord!.VerificationStatus, Is.EqualTo(KycVerificationStatus.Unverified));

        profile.SubmitKycForVerification("001202000001", new DateOnly(2021, 5, 20), "front.png", "back.png", DateTimeOffset.UtcNow);
        Assert.That(profile.Landlord.VerificationStatus, Is.EqualTo(KycVerificationStatus.Pending));

        profile.ApproveKyc(DateTimeOffset.UtcNow, "Valid CCCD");
        Assert.That(profile.Landlord.VerificationStatus, Is.EqualTo(KycVerificationStatus.Verified));
        Assert.That(profile.Landlord.VerifiedAt, Is.Not.Null);
    }
}