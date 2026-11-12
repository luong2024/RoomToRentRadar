using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IGetMyProfileUseCase
{
    Task<UserProfileDto> ExecuteAsync(Guid userId, string role, CancellationToken cancellationToken = default);
}

public interface IUpdateTenantProfileUseCase
{
    Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateTenantCommand command, CancellationToken cancellationToken = default);
}

public interface IUpdateLandlordProfileUseCase
{
    Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateLandlordCommand command, CancellationToken cancellationToken = default);
}

public interface IUpdateAdminProfileUseCase
{
    Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateAdminCommand command, CancellationToken cancellationToken = default);
}

public interface IVerifyLandlordKycUseCase
{
    Task<UserProfileDto> ExecuteAsync(VerifyLandlordKycCommand command, CancellationToken cancellationToken = default);
}

public interface IGetPublicProfileUseCase
{
    Task<PublicProfileDto> ExecuteAsync(Guid targetUserId, CancellationToken cancellationToken = default);
}

public record UpdateTenantCommand(
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

public record UpdateLandlordCommand(
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Bio,
    string? BusinessName,
    string? Address,
    string? CitizenIdNumber,
    DateOnly? CitizenIdIssueDate,
    string? IdentityFrontPhotoUrl,
    string? IdentityBackPhotoUrl,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountHolder);

public record UpdateAdminCommand(string FullName, string? PhoneNumber, string StaffCode, string Department, string Title);

public record VerifyLandlordKycCommand(Guid TargetUserId, bool IsApproved, string? Reason);