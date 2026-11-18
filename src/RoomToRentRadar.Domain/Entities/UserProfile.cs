using RoomToRentRadar.Domain.Common;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Entities;

public sealed class UserProfile
{
    private const int MaxAvatarBytes = 1024 * 1024;
    private const int MaxAvatarBase64Length = ((MaxAvatarBytes + 2) / 3) * 4;
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
    public string? Version { get; private set; }

    private UserProfile() { }

    public void SetVersion(string? version)
    {
        Version = version;
    }

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
            profile.Admin = null; // Left unassigned until actual administrative staff data is provided

        return profile;
    }

    public void UpdateBasicInfo(string fullName, PhoneNumber? phone, string? avatarUrl, string? bio, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(fullName?.Trim()))
        {
            throw new ProfileValidationException("Full name cannot be empty.");
        }

        ValidateAvatarUrl(avatarUrl);

        FullName = fullName.Trim();
        PhoneNumber = phone;
        AvatarUrl = avatarUrl;
        Bio = bio;
        UpdatedAt = utcNow;
    }

    public static void ValidateAvatarUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        if (Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return;
        }

        const string pngPrefix = "data:image/png;base64,";
        const string jpegPrefix = "data:image/jpeg;base64,";
        var prefix = avatarUrl.StartsWith(pngPrefix, StringComparison.OrdinalIgnoreCase)
            ? pngPrefix
            : avatarUrl.StartsWith(jpegPrefix, StringComparison.OrdinalIgnoreCase)
                ? jpegPrefix
                : null;

        if (prefix is null)
        {
            throw new ProfileValidationException("Avatar must be an HTTPS URL or a PNG/JPEG data URI.");
        }

        var base64 = avatarUrl[prefix.Length..];
        if (base64.Length > MaxAvatarBase64Length)
        {
            throw new ProfileValidationException("Avatar image must be 1 MB or smaller.");
        }

        try
        {
            if (Convert.FromBase64String(base64).Length > MaxAvatarBytes)
            {
                throw new ProfileValidationException("Avatar image must be 1 MB or smaller.");
            }
        }
        catch (FormatException)
        {
            throw new ProfileValidationException("Avatar data URI contains invalid Base64 data.");
        }
    }

    public void RehydrateBasicInfo(string fullName, PhoneNumber? phone, string? avatarUrl, string? bio, DateTimeOffset utcNow)
    {
        FullName = fullName ?? string.Empty;
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

        ArgumentNullException.ThrowIfNull(details);

        // Security invariant: If KYC identity documents changed on an already-verified profile,
        // status cannot remain Verified without review.
        if (Landlord is not null &&
            Landlord.VerificationStatus == KycVerificationStatus.Verified &&
            (Landlord.CitizenIdNumber != details.CitizenIdNumber ||
             Landlord.CitizenIdIssueDate != details.CitizenIdIssueDate ||
             Landlord.IdentityFrontBlobName != details.IdentityFrontBlobName ||
             Landlord.IdentityBackBlobName != details.IdentityBackBlobName))
        {
            if (details.VerificationStatus == KycVerificationStatus.Verified)
            {
                throw new KycOperationInvalidException("Cannot modify KYC identity documents while retaining Verified status.");
            }
        }

        Landlord = details;
        UpdatedAt = utcNow;
    }

    public void SubmitKycForVerification(
        string citizenIdNumber,
        DateOnly issueDate,
        string frontBlobName,
        string backBlobName,
        DateTimeOffset utcNow)
    {
        if (Role != UserRole.Landlord || Landlord is null)
            throw new ProfileRoleMismatchException("Only landlords can submit KYC verification.");

        if (string.IsNullOrWhiteSpace(citizenIdNumber))
            throw new KycOperationInvalidException("Citizen ID number is required for KYC submission.");

        var today = BusinessTime.ToBusinessDate(utcNow);
        if (issueDate > today)
            throw new KycOperationInvalidException("Citizen ID issue date cannot be in the future.");

        if (string.IsNullOrWhiteSpace(frontBlobName) || string.IsNullOrWhiteSpace(backBlobName))
            throw new KycOperationInvalidException("Both front and back identity photos are required for KYC submission.");

        Landlord = Landlord with
        {
            CitizenIdNumber = citizenIdNumber.Trim(),
            CitizenIdIssueDate = issueDate,
            IdentityFrontBlobName = frontBlobName.Trim(),
            IdentityBackBlobName = backBlobName.Trim(),
            VerificationStatus = KycVerificationStatus.Pending,
            VerificationNotes = "Pending review by administrator.",
            VerifiedAt = null
        };
        UpdatedAt = utcNow;
    }

    public void ApproveKyc(DateTimeOffset utcNow, string? note = null)
    {
        if (Role != UserRole.Landlord || Landlord is null)
            throw new ProfileRoleMismatchException("Only landlords have KYC records.");

        if (Landlord.VerificationStatus != KycVerificationStatus.Pending)
            throw new KycOperationInvalidException($"Cannot approve KYC verification for landlord in status '{Landlord.VerificationStatus}'. Landlord must be in 'Pending' status.");

        if (string.IsNullOrWhiteSpace(Landlord.CitizenIdNumber) ||
            !Landlord.CitizenIdIssueDate.HasValue ||
            string.IsNullOrWhiteSpace(Landlord.IdentityFrontBlobName) ||
            string.IsNullOrWhiteSpace(Landlord.IdentityBackBlobName))
        {
            throw new KycOperationInvalidException("Cannot approve KYC verification without complete documents (Citizen ID number, issue date, front photo, back photo).");
        }

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

        if (Landlord.VerificationStatus != KycVerificationStatus.Pending)
            throw new KycOperationInvalidException($"Cannot reject KYC verification for landlord in status '{Landlord.VerificationStatus}'. Landlord must be in 'Pending' status.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new KycOperationInvalidException("Rejection reason cannot be empty.");

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
