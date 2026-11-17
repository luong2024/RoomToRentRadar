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
            Assert.That(profile.Admin, Is.Null);
        });
    }

    [Test]
    public void CreateDefault_ForAdmin_ShouldLeaveAdminDetailsUnsetUntilUpdated()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Admin, DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(profile.Admin, Is.Null);
            Assert.That(profile.Tenant, Is.Null);
            Assert.That(profile.Landlord, Is.Null);
        });

        profile.UpdateAdminDetails(new AdminDetails("ADMIN-01", "Security", "Operations Lead"), DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Admin, Is.Not.Null);
            Assert.That(profile.Admin!.StaffCode, Is.EqualTo("ADMIN-01"));
            Assert.That(profile.Admin.Department, Is.EqualTo("Security"));
            Assert.That(profile.Admin.Title, Is.EqualTo("Operations Lead"));
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

    [Test]
    public void ApproveKyc_WhenStatusIsNotPending_ShouldThrowKycOperationInvalidException()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, DateTimeOffset.UtcNow);
        Assert.That(profile.Landlord!.VerificationStatus, Is.EqualTo(KycVerificationStatus.Unverified));

        var ex = Assert.Throws<KycOperationInvalidException>(() =>
            profile.ApproveKyc(DateTimeOffset.UtcNow));

        Assert.That(ex.Message, Does.Contain("must be in 'Pending' status"));
    }

    [Test]
    public void RejectKyc_WhenStatusIsNotPending_ShouldThrowKycOperationInvalidException()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, DateTimeOffset.UtcNow);
        Assert.That(profile.Landlord!.VerificationStatus, Is.EqualTo(KycVerificationStatus.Unverified));

        var ex = Assert.Throws<KycOperationInvalidException>(() =>
            profile.RejectKyc("Invalid document", DateTimeOffset.UtcNow));

        Assert.That(ex.Message, Does.Contain("must be in 'Pending' status"));
    }

    [Test]
    public void RejectKyc_WhenReasonIsEmpty_ShouldThrowKycOperationInvalidException()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, DateTimeOffset.UtcNow);
        profile.SubmitKycForVerification("001202000001", new DateOnly(2021, 5, 20), "front.png", "back.png", DateTimeOffset.UtcNow);

        var ex = Assert.Throws<KycOperationInvalidException>(() =>
            profile.RejectKyc("   ", DateTimeOffset.UtcNow));

        Assert.That(ex.Message, Does.Contain("reason cannot be empty"));
    }

    [Test]
    public void UpdateLandlordDetails_WhenVerifiedLandlordAltersKycIdentity_ShouldThrowKycOperationInvalidException()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, DateTimeOffset.UtcNow);
        profile.SubmitKycForVerification("001202000001", new DateOnly(2021, 5, 20), "front.png", "back.png", DateTimeOffset.UtcNow);
        profile.ApproveKyc(DateTimeOffset.UtcNow, "Approved");
        Assert.That(profile.Landlord!.VerificationStatus, Is.EqualTo(KycVerificationStatus.Verified));

        // Try to update CCCD number while keeping status Verified
        var tamperedDetails = profile.Landlord with
        {
            CitizenIdNumber = "999999999999",
            VerificationStatus = KycVerificationStatus.Verified
        };

        var ex = Assert.Throws<KycOperationInvalidException>(() =>
            profile.UpdateLandlordDetails(tamperedDetails, DateTimeOffset.UtcNow));

        Assert.That(ex.Message, Does.Contain("Cannot modify KYC identity documents while retaining Verified status"));
    }

    [Test]
    public void SubmitKycForVerification_WhenVietnamTimeMatchesTodayEvenIfUtcIsPreviousDay_ShouldSucceed()
    {
        // 2026-11-12 20:30:00 UTC is 2026-11-13 03:30:00 AM ICT (Vietnam)
        var earlyMorningVietnamUtc = new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero);
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, earlyMorningVietnamUtc);

        // Issue date is today in Vietnam
        var vietnamToday = new DateOnly(2026, 11, 13);
        Assert.DoesNotThrow(() =>
            profile.SubmitKycForVerification("001202000002", vietnamToday, "front.jpg", "back.jpg", earlyMorningVietnamUtc));

        Assert.That(profile.Landlord!.VerificationStatus, Is.EqualTo(KycVerificationStatus.Pending));
    }

    [Test]
    public void SubmitKycForVerification_WhenIssueDateIsInFutureInVietnamTime_ShouldThrowKycOperationInvalidException()
    {
        var earlyMorningVietnamUtc = new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero);
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Landlord, earlyMorningVietnamUtc);

        var tomorrowInVietnam = new DateOnly(2026, 11, 14);
        var ex = Assert.Throws<KycOperationInvalidException>(() =>
            profile.SubmitKycForVerification("001202000002", tomorrowInVietnam, "front.jpg", "back.jpg", earlyMorningVietnamUtc));

        Assert.That(ex.Message, Does.Contain("Citizen ID issue date cannot be in the future"));
    }

    [Test]
    public void UpdateBasicInfo_WhenFullNameIsEmpty_ShouldThrowProfileValidationException()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Student, DateTimeOffset.UtcNow);

        var ex = Assert.Throws<ProfileValidationException>(() =>
            profile.UpdateBasicInfo("   ", null, null, null, DateTimeOffset.UtcNow));

        Assert.That(ex.Message, Does.Contain("Full name cannot be empty"));
    }

    [Test]
    public void RehydrateBasicInfo_WhenFullNameIsEmpty_ShouldNotThrowAndPreserveState()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), UserRole.Student, DateTimeOffset.UtcNow);

        Assert.DoesNotThrow(() =>
            profile.RehydrateBasicInfo(string.Empty, null, "https://avatar.png", "bio", DateTimeOffset.UtcNow));

        Assert.Multiple(() =>
        {
            Assert.That(profile.FullName, Is.Empty);
            Assert.That(profile.AvatarUrl, Is.EqualTo("https://avatar.png"));
            Assert.That(profile.Bio, Is.EqualTo("bio"));
        });
    }
}
