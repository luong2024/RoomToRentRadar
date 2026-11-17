using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.ValueObjects;

public sealed record TenantDetails
{
    public string? StudentId { get; init; }
    public string? University { get; init; }
    public string? Faculty { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public Gender Gender { get; init; }
    public string? EmergencyContactName { get; init; }
    public PhoneNumber? EmergencyContactPhone { get; init; }
    public decimal? MaxBudget { get; init; }

    public TenantDetails(
        string? studentId,
        string? university,
        string? faculty,
        DateOnly? dateOfBirth,
        Gender gender,
        string? emergencyContactName,
        PhoneNumber? emergencyContactPhone,
        decimal? maxBudget)
    {
        if (!Enum.IsDefined(typeof(Gender), gender))
        {
            throw new ProfileValidationException($"Invalid gender value '{(int)gender}'.");
        }

        if (maxBudget.HasValue && maxBudget.Value < 0)
        {
            throw new ProfileValidationException("Maximum budget cannot be negative.");
        }

        StudentId = studentId;
        University = university;
        Faculty = faculty;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        EmergencyContactName = emergencyContactName;
        EmergencyContactPhone = emergencyContactPhone;
        MaxBudget = maxBudget;
    }
}
