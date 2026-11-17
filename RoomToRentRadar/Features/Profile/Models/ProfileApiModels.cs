namespace RoomToRentRadar.Features.Profile.Models;

public sealed class ProfileApiDto
{
    public string Role { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public TenantProfileDto? Tenant { get; set; }
    public LandlordProfileDto? Landlord { get; set; }
    public AdminProfileDto? Admin { get; set; }
}

public sealed class TenantProfileDto
{
    public string? StudentId { get; set; }
    public string? University { get; set; }
    public string? Faculty { get; set; }
    public decimal? MaxBudget { get; set; }
}

public sealed class LandlordProfileDto
{
    public string? BusinessName { get; set; }
    public string? Address { get; set; }
    public string? VerificationStatus { get; set; }
}

public sealed class AdminProfileDto
{
    public string? StaffCode { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
}

public sealed record UpdateTenantProfileRequest(string FullName, string? PhoneNumber, string? AvatarUrl, string? Bio,
    string? StudentId, string? University, string? Faculty, DateOnly? DateOfBirth, string? Gender,
    string? EmergencyContactName, string? EmergencyContactPhone, decimal? MaxBudget);

public sealed record UpdateLandlordProfileRequest(string FullName, string? PhoneNumber, string? AvatarUrl, string? Bio,
    string? BusinessName, string? Address, string? CitizenIdNumber, DateOnly? CitizenIdIssueDate,
    string? IdentityFrontBlobName, string? IdentityBackBlobName, string? BankName, string? BankAccountNumber, string? BankAccountHolder);

public sealed record UpdateAdminProfileRequest(string FullName, string? PhoneNumber, string StaffCode, string Department,
    string Title, string? AvatarUrl, string? Bio);
