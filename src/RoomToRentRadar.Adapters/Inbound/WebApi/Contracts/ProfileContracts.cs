namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed record UpdateTenantProfileRequest(
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Bio,
    string? StudentId,
    string? University,
    string? Faculty,
    DateOnly? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    decimal? MaxBudget);

public sealed record UpdateLandlordProfileRequest(
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Bio,
    string? BusinessName,
    string? Address,
    string? CitizenIdNumber,
    DateOnly? CitizenIdIssueDate,
    string? IdentityFrontBlobName,
    string? IdentityBackBlobName,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder);

public sealed record UpdateAdminProfileRequest(
    string FullName,
    string? PhoneNumber,
    string StaffCode,
    string Department,
    string Title,
    string? AvatarUrl = null,
    string? Bio = null);

public sealed record VerifyLandlordKycRequest(bool IsApproved, string? Reason);
