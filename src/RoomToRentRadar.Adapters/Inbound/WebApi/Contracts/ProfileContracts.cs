using System.Text.Json.Serialization;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed class UpdateTenantProfileRequest
{
    public UpdateTenantProfileRequest()
    {
    }

    public UpdateTenantProfileRequest(
        string FullName,
        string? PhoneNumber = null,
        string? AvatarUrl = null,
        string? Bio = null,
        string? StudentId = null,
        string? University = null,
        string? Faculty = null,
        DateOnly? DateOfBirth = null,
        string? Gender = null,
        string? EmergencyContactName = null,
        string? EmergencyContactPhone = null,
        decimal? MaxBudget = null)
    {
        this.FullName = FullName;
        this.PhoneNumber = PhoneNumber;
        this.AvatarUrl = AvatarUrl;
        this.Bio = Bio;
        this.StudentId = StudentId;
        this.University = University;
        this.Faculty = Faculty;
        this.DateOfBirth = DateOfBirth;
        this.Gender = Gender;
        this.EmergencyContactName = EmergencyContactName;
        this.EmergencyContactPhone = EmergencyContactPhone;
        this.MaxBudget = MaxBudget;
    }

    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }

    private string? _studentId;
    public string? StudentId
    {
        get => _studentId;
        set
        {
            _studentId = value;
            IsStudentIdSpecified = true;
        }
    }

    private string? _university;
    public string? University
    {
        get => _university;
        set
        {
            _university = value;
            IsUniversitySpecified = true;
        }
    }

    private string? _faculty;
    public string? Faculty
    {
        get => _faculty;
        set
        {
            _faculty = value;
            IsFacultySpecified = true;
        }
    }

    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    private decimal? _maxBudget;
    public decimal? MaxBudget
    {
        get => _maxBudget;
        set
        {
            _maxBudget = value;
            IsMaxBudgetSpecified = true;
        }
    }

    [JsonIgnore] public bool IsStudentIdSpecified { get; private set; }
    [JsonIgnore] public bool IsUniversitySpecified { get; private set; }
    [JsonIgnore] public bool IsFacultySpecified { get; private set; }
    [JsonIgnore] public bool IsMaxBudgetSpecified { get; private set; }
}

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
