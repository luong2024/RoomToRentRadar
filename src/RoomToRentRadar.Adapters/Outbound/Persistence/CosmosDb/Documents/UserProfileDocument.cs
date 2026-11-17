using System.Text.Json.Serialization;
using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using PhoneNumberValueObject = RoomToRentRadar.Domain.ValueObjects.PhoneNumber;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class UserProfileDocument
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("role")]
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("fullName")]
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonProperty("phoneNumber")]
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonProperty("avatarUrl")]
    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonProperty("bio")]
    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonProperty("tenant")]
    [JsonPropertyName("tenant")]
    public TenantSubDocument? Tenant { get; set; }

    [JsonProperty("landlord")]
    [JsonPropertyName("landlord")]
    public LandlordSubDocument? Landlord { get; set; }

    [JsonProperty("admin")]
    [JsonPropertyName("admin")]
    public AdminSubDocument? Admin { get; set; }

    [JsonProperty("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonProperty("updatedAt")]
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonProperty("_etag", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    public static UserProfileDocument FromDomain(UserProfile entity) =>
        new()
        {
            Id = entity.UserId.ToString(),
            UserId = entity.UserId.ToString(),
            Role = entity.Role.ToRoleName(),
            FullName = entity.FullName,
            PhoneNumber = entity.PhoneNumber?.Value,
            AvatarUrl = entity.AvatarUrl,
            Bio = entity.Bio,
            Tenant = entity.Tenant is null ? null : new TenantSubDocument
            {
                StudentId = entity.Tenant.StudentId,
                University = entity.Tenant.University,
                Faculty = entity.Tenant.Faculty,
                DateOfBirth = entity.Tenant.DateOfBirth,
                Gender = entity.Tenant.Gender.ToString(),
                EmergencyContactName = entity.Tenant.EmergencyContactName,
                EmergencyContactPhone = entity.Tenant.EmergencyContactPhone?.Value,
                MaxBudget = entity.Tenant.MaxBudget
            },
            Landlord = entity.Landlord is null ? null : new LandlordSubDocument
            {
                BusinessName = entity.Landlord.BusinessName,
                Address = entity.Landlord.Address,
                CitizenIdNumber = entity.Landlord.CitizenIdNumber,
                CitizenIdIssueDate = entity.Landlord.CitizenIdIssueDate,
                IdentityFrontBlobName = entity.Landlord.IdentityFrontBlobName,
                IdentityBackBlobName = entity.Landlord.IdentityBackBlobName,
                VerificationStatus = entity.Landlord.VerificationStatus.ToString(),
                VerificationNotes = entity.Landlord.VerificationNotes,
                VerifiedAt = entity.Landlord.VerifiedAt,
                BankName = entity.Landlord.BankName,
                BankAccountNumber = entity.Landlord.BankAccountNumber,
                BankAccountHolder = entity.Landlord.BankAccountHolder,
                Rating = entity.Landlord.Rating,
                ReviewCount = entity.Landlord.ReviewCount
            },
            Admin = entity.Admin is null ? null : new AdminSubDocument
            {
                StaffCode = entity.Admin.StaffCode,
                Department = entity.Admin.Department,
                Title = entity.Admin.Title
            },
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ETag = entity.Version
        };

    public UserProfile ToDomain()
    {
        var role = UserRoleExtensions.ParseRole(Role);
        var profile = UserProfile.CreateDefault(Guid.Parse(UserId), role, CreatedAt);
        profile.SetVersion(ETag);
        profile.RehydrateBasicInfo(FullName, PhoneNumberValueObject.CreateOptional(PhoneNumber), AvatarUrl, Bio, UpdatedAt);

        if (Tenant is not null && role == UserRole.Student)
        {
            var gender = Enum.TryParse<Gender>(Tenant.Gender, true, out var g) ? g : Gender.Unspecified;
            profile.UpdateTenantDetails(new TenantDetails(
                Tenant.StudentId,
                Tenant.University,
                Tenant.Faculty,
                Tenant.DateOfBirth,
                gender,
                Tenant.EmergencyContactName,
                PhoneNumberValueObject.CreateOptional(Tenant.EmergencyContactPhone),
                Tenant.MaxBudget), UpdatedAt);
        }

        if (Landlord is not null && role == UserRole.Landlord)
        {
            var status = Enum.TryParse<KycVerificationStatus>(Landlord.VerificationStatus, true, out var s) ? s : KycVerificationStatus.Unverified;
            profile.UpdateLandlordDetails(new LandlordDetails(
                Landlord.BusinessName,
                Landlord.Address,
                Landlord.CitizenIdNumber,
                Landlord.CitizenIdIssueDate,
                Landlord.IdentityFrontBlobName ?? Landlord.IdentityFrontPhotoUrl,
                Landlord.IdentityBackBlobName ?? Landlord.IdentityBackPhotoUrl,
                status,
                Landlord.VerificationNotes,
                Landlord.VerifiedAt,
                Landlord.BankName,
                Landlord.BankAccountNumber,
                Landlord.BankAccountHolder,
                Landlord.Rating,
                Landlord.ReviewCount), UpdatedAt);
        }

        if (Admin is not null && role == UserRole.Admin)
        {
            profile.UpdateAdminDetails(new AdminDetails(Admin.StaffCode, Admin.Department, Admin.Title), UpdatedAt);
        }

        return profile;
    }
}

public sealed class TenantSubDocument
{
    [JsonProperty("studentId")]
    public string? StudentId { get; set; }
    [JsonProperty("university")]
    public string? University { get; set; }
    [JsonProperty("faculty")]
    public string? Faculty { get; set; }
    [JsonProperty("dateOfBirth")]
    public DateOnly? DateOfBirth { get; set; }
    [JsonProperty("gender")]
    public string Gender { get; set; } = "Unspecified";
    [JsonProperty("emergencyContactName")]
    public string? EmergencyContactName { get; set; }
    [JsonProperty("emergencyContactPhone")]
    public string? EmergencyContactPhone { get; set; }
    [JsonProperty("maxBudget")]
    public decimal? MaxBudget { get; set; }
}

public sealed class LandlordSubDocument
{
    [JsonProperty("businessName")]
    public string? BusinessName { get; set; }
    [JsonProperty("address")]
    public string? Address { get; set; }
    [JsonProperty("citizenIdNumber")]
    public string? CitizenIdNumber { get; set; }
    [JsonProperty("citizenIdIssueDate")]
    public DateOnly? CitizenIdIssueDate { get; set; }
    [JsonProperty("identityFrontBlobName")]
    [JsonPropertyName("identityFrontBlobName")]
    public string? IdentityFrontBlobName { get; set; }
    [JsonProperty("identityBackBlobName")]
    [JsonPropertyName("identityBackBlobName")]
    public string? IdentityBackBlobName { get; set; }
    [JsonProperty("identityFrontPhotoUrl")]
    [JsonPropertyName("identityFrontPhotoUrl")]
    public string? IdentityFrontPhotoUrl { get; set; }
    [JsonProperty("identityBackPhotoUrl")]
    [JsonPropertyName("identityBackPhotoUrl")]
    public string? IdentityBackPhotoUrl { get; set; }
    [JsonProperty("verificationStatus")]
    public string VerificationStatus { get; set; } = "Unverified";
    [JsonProperty("verificationNotes")]
    public string? VerificationNotes { get; set; }
    [JsonProperty("verifiedAt")]
    public DateTimeOffset? VerifiedAt { get; set; }
    [JsonProperty("bankName")]
    public string? BankName { get; set; }
    [JsonProperty("bankAccountNumber")]
    public string? BankAccountNumber { get; set; }
    [JsonProperty("bankAccountHolder")]
    public string? BankAccountHolder { get; set; }
    [JsonProperty("rating")]
    public double Rating { get; set; } = 5.0;
    [JsonProperty("reviewCount")]
    public int ReviewCount { get; set; }
}

public sealed class AdminSubDocument
{
    [JsonProperty("staffCode")]
    public string? StaffCode { get; set; }
    [JsonProperty("department")]
    public string? Department { get; set; }
    [JsonProperty("title")]
    public string? Title { get; set; }
}
