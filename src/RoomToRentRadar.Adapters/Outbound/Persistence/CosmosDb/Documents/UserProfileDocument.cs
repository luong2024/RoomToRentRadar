using System.Text.Json.Serialization;
using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using PhoneNumberValueObject = RoomToRentRadar.Domain.ValueObjects.PhoneNumber;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class UserProfileDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("tenant")]
    public TenantSubDocument? Tenant { get; set; }

    [JsonPropertyName("landlord")]
    public LandlordSubDocument? Landlord { get; set; }

    [JsonPropertyName("admin")]
    public AdminSubDocument? Admin { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

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
    public string? StudentId { get; set; }
    public string? University { get; set; }
    public string? Faculty { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = "Unspecified";
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public decimal? MaxBudget { get; set; }
}

public sealed class LandlordSubDocument
{
    public string? BusinessName { get; set; }
    public string? Address { get; set; }
    public string? CitizenIdNumber { get; set; }
    public DateOnly? CitizenIdIssueDate { get; set; }
    [JsonPropertyName("identityFrontBlobName")]
    public string? IdentityFrontBlobName { get; set; }
    [JsonPropertyName("identityBackBlobName")]
    public string? IdentityBackBlobName { get; set; }
    [JsonPropertyName("identityFrontPhotoUrl")]
    public string? IdentityFrontPhotoUrl { get; set; }
    [JsonPropertyName("identityBackPhotoUrl")]
    public string? IdentityBackPhotoUrl { get; set; }
    public string VerificationStatus { get; set; } = "Unverified";
    public string? VerificationNotes { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountHolder { get; set; }
    public double Rating { get; set; } = 5.0;
    public int ReviewCount { get; set; }
}

public sealed class AdminSubDocument
{
    public string? StaffCode { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
}
