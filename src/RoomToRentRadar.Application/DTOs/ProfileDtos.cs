using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.DTOs;

public sealed record UserProfileDto(
    Guid UserId,
    string Role,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Bio,
    TenantDetailsDto? Tenant,
    LandlordDetailsDto? Landlord,
    AdminDetailsDto? Admin);

public sealed record TenantDetailsDto(
    string? StudentId,
    string? University,
    string? Faculty,
    DateOnly? DateOfBirth,
    string Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    decimal? MaxBudget);

public sealed record LandlordDetailsDto(
    string? BusinessName,
    string? Address,
    string? CitizenIdNumber,
    DateOnly? CitizenIdIssueDate,
    string? IdentityFrontPhotoUrl,
    string? IdentityBackPhotoUrl,
    string VerificationStatus,
    string? VerificationNotes,
    DateTimeOffset? VerifiedAt,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder,
    double Rating,
    int ReviewCount);

public sealed record AdminDetailsDto(
    string? StaffCode,
    string? Department,
    string? Title);

public sealed record AdminLandlordKycDto(
    Guid UserId,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? BusinessName,
    string? Address,
    string? CitizenIdNumber,
    DateOnly? CitizenIdIssueDate,
    string? IdentityFrontPhotoUrl,
    string? IdentityBackPhotoUrl,
    string VerificationStatus,
    string? VerificationNotes,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PublicProfileDto(
    Guid UserId,
    string Role,
    string FullName,
    string? AvatarUrl,
    string? Bio,
    string? BusinessName,
    string? University,
    double Rating,
    int ReviewCount,
    bool IsVerified);

public sealed record CreateKycUploadUrlRequest(
    string DocumentSide,
    string ContentType);

public sealed record KycUploadUrlResponse(
    string BlobName,
    string UploadUrl,
    DateTimeOffset ExpiresAt);

public static class SensitiveDataMasker
{
    public static string? Mask(string? value, int visibleSuffix = 4)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        if (trimmed.Length <= visibleSuffix)
            return new string('*', trimmed.Length);

        var maskedLength = trimmed.Length - visibleSuffix;
        return string.Concat(new string('*', maskedLength), trimmed[^visibleSuffix..]);
    }
}
