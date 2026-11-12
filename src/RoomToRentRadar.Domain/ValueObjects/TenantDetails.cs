using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Domain.ValueObjects;

public sealed record TenantDetails(
    string? StudentId,
    string? University,
    string? Faculty,
    DateOnly? DateOfBirth,
    Gender Gender,
    string? EmergencyContactName,
    PhoneNumber? EmergencyContactPhone,
    decimal? MaxBudget);