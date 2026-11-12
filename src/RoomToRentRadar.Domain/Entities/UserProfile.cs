using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Entities;

public sealed class UserProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public UserRole Role { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public PhoneNumber? PhoneNumber { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public TenantDetails? Tenant { get; private set; }
    public LandlordDetails? Landlord { get; private set; }
    public AdminDetails? Admin { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile() { }

    public static UserProfile CreateDefault(Guid userId, UserRole role, DateTimeOffset utcNow)
    {
        var profile = new UserProfile
        {
            Id = userId,
            UserId = userId,
            Role = role,
            FullName = string.Empty,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        if (role == UserRole.Student)
            profile.Tenant = new TenantDetails(null, "ICTU", null, null, Gender.Unspecified, null, null, null);
        else if (role == UserRole.Landlord)
            profile.Landlord = new LandlordDetails(null, null, null, null, null, null, KycVerificationStatus.Unverified, null, null, null, null, null);
        else if (role == UserRole.Admin)
            profile.Admin = new AdminDetails("STAFF-01", "Administration", "System Admin");

        return profile;
    }

    public void UpdateBasicInfo(string fullName, PhoneNumber? phone, string? avatarUrl, string? bio, DateTimeOffset utcNow)
    {
        FullName = fullName?.Trim() ?? string.Empty;
        PhoneNumber = phone;
        AvatarUrl = avatarUrl;
        Bio = bio;
        UpdatedAt = utcNow;
    }

    public void UpdateTenantDetails(TenantDetails details, DateTimeOffset utcNow)
    {
        if (Role != UserRole.Student)
            throw new ProfileRoleMismatchException("Only users with role STUDENT can update tenant details.");

        Tenant = details ?? throw new ArgumentNullException(nameof(details));
        UpdatedAt = utcNow;
    }

    public void UpdateLandlordDetails(LandlordDetails details, DateTimeOffset utcNow)
    {
        if (Role != UserRole.Landlord)
            throw new ProfileRoleMismatchException("Only users with role LANDLORD can update landlord details.");

        Landlord = details ?? throw new ArgumentNullException(nameof(details));
        UpdatedAt = utcNow;
    }

    public void SubmitKycForVerification(
        string citizenIdNumber,
        DateOnly issueDate,
        string frontPhotoUrl,
        string backPhotoUrl,
        DateTimeOffset utcNow)
    {
        if (Role != UserRole.Landlord || Landlord is null)
            throw new ProfileRoleMismatchException("Only landlords can submit KYC verification.");

        Landlord = Landlord with
        {
            CitizenIdNumber = citizenIdNumber,
            CitizenIdIssueDate = issueDate,
            IdentityFrontPhotoUrl = frontPhotoUrl,
            IdentityBackPhotoUrl = backPhotoUrl,
            VerificationStatus = KycVerificationStatus.Pending,
            VerificationNotes = "Pending review by administrator."
        };
        UpdatedAt = utcNow;
    }

    public void ApproveKyc(DateTimeOffset utcNow, string? note = null)
    {
        if (Role != UserRole.Landlord || Landlord is null)
            throw new ProfileRoleMismatchException("Only landlords have KYC records.");

        Landlord = Landlord with
        {
            VerificationStatus = KycVerificationStatus.Verified,
            VerifiedAt = utcNow,
            VerificationNotes = note ?? "Approved by administrator."
        };
        UpdatedAt = utcNow;
    }

    public void RejectKyc(string reason, DateTimeOffset utcNow)
    {
        if (Role != UserRole.Landlord || Landlord is null)
            throw new ProfileRoleMismatchException("Only landlords have KYC records.");

        Landlord = Landlord with
        {
            VerificationStatus = KycVerificationStatus.Rejected,
            VerificationNotes = reason,
            VerifiedAt = null
        };
        UpdatedAt = utcNow;
    }

    public void UpdateAdminDetails(AdminDetails details, DateTimeOffset utcNow)
    {
        if (Role != UserRole.Admin)
            throw new ProfileRoleMismatchException("Only administrators can update admin details.");

        Admin = details ?? throw new ArgumentNullException(nameof(details));
        UpdatedAt = utcNow;
    }
}