using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Domain.ValueObjects;

public sealed record LandlordDetails(
    string? BusinessName,
    string? Address,
    string? CitizenIdNumber,
    DateOnly? CitizenIdIssueDate,
    string? IdentityFrontPhotoUrl,
    string? IdentityBackPhotoUrl,
    KycVerificationStatus VerificationStatus,
    string? VerificationNotes,
    DateTimeOffset? VerifiedAt,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder,
    double Rating = 5.0,
    int ReviewCount = 0);