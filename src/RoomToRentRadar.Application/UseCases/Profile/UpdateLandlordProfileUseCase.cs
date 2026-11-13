using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Common;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class UpdateLandlordProfileUseCase : IUpdateLandlordProfileUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly IKycDocumentStorageService _kycStorage;
    private readonly TimeProvider _timeProvider;

    public UpdateLandlordProfileUseCase(
        IUserProfileRepository repository,
        IKycDocumentStorageService kycStorage,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _kycStorage = kycStorage ?? throw new ArgumentNullException(nameof(kycStorage));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateLandlordCommand command, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var existingProfile = await _repository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var isNewProfile = existingProfile is null;

        var profile = existingProfile
            ?? UserProfile.CreateDefault(
                userId,
                UserRole.Landlord,
                now);

        var phone = PhoneNumber.CreateOptional(command.PhoneNumber);
        profile.UpdateBasicInfo(command.FullName, phone, command.AvatarUrl, command.Bio, now);

        var currentLandlord = profile.Landlord;

        var isSubmittingKyc = !string.IsNullOrWhiteSpace(command.CitizenIdNumber) ||
                              command.CitizenIdIssueDate.HasValue ||
                              !string.IsNullOrWhiteSpace(command.IdentityFrontBlobName) ||
                              !string.IsNullOrWhiteSpace(command.IdentityBackBlobName);

        var kycChanged = currentLandlord is null ||
            !string.Equals(currentLandlord.CitizenIdNumber, command.CitizenIdNumber, StringComparison.Ordinal) ||
            currentLandlord.CitizenIdIssueDate != command.CitizenIdIssueDate ||
            !string.Equals(currentLandlord.IdentityFrontBlobName, command.IdentityFrontBlobName, StringComparison.Ordinal) ||
            !string.Equals(currentLandlord.IdentityBackBlobName, command.IdentityBackBlobName, StringComparison.Ordinal);

        var rollbackBlobs = new List<string>();

        if (isSubmittingKyc && kycChanged)
        {
            if (string.IsNullOrWhiteSpace(command.CitizenIdNumber) ||
                !command.CitizenIdIssueDate.HasValue ||
                string.IsNullOrWhiteSpace(command.IdentityFrontBlobName) ||
                string.IsNullOrWhiteSpace(command.IdentityBackBlobName))
            {
                throw new KycOperationInvalidException(
                    "All KYC documents (Citizen ID number, issue date, front photo blob, and back photo blob) must be provided together for verification.");
            }

            var today = BusinessTime.ToBusinessDate(now);
            if (command.CitizenIdIssueDate.Value > today)
            {
                throw new KycOperationInvalidException("Citizen ID issue date cannot be in the future.");
            }

            try
            {
                if (string.Equals(command.IdentityFrontBlobName, command.IdentityBackBlobName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new KycOperationInvalidException("Front and back KYC document blobs must be different.");
                }

                var verifiedFrontBlobName = await _kycStorage.ValidateAndPromoteUploadedDocumentAsync(
                    userId, "front", command.IdentityFrontBlobName, cancellationToken);
                rollbackBlobs.Add(verifiedFrontBlobName);

                var verifiedBackBlobName = await _kycStorage.ValidateAndPromoteUploadedDocumentAsync(
                    userId, "back", command.IdentityBackBlobName, cancellationToken);
                rollbackBlobs.Add(verifiedBackBlobName);

                profile.SubmitKycForVerification(
                    command.CitizenIdNumber,
                    command.CitizenIdIssueDate.Value,
                    verifiedFrontBlobName,
                    verifiedBackBlobName,
                    now);
            }
            catch (Exception ex)
            {
                await CompensatePromotedBlobsAsync(rollbackBlobs);

                if (ex is RateLimitExceededException or KycOperationInvalidException)
                {
                    throw;
                }

                // Infrastructure, storage, network, or server failures must NOT be converted to client 400 Bad Request.
                // If the exception or its inner exception represents an infrastructure/network/storage outage
                // (excluding 404 Not Found which indicates client did not upload the blob), rethrow directly for HTTP 500.
                if (IsInfrastructureException(ex))
                {
                    throw;
                }

                if (ex is ArgumentException or InvalidOperationException or ProfileDomainException)
                {
                    throw new KycOperationInvalidException($"Invalid KYC document: {ex.Message}", ex);
                }

                // Infrastructure, storage, network, or server failures must NOT be converted to client 400 Bad Request.
                // Rethrow directly so GlobalExceptionHandler maps them to HTTP 500 Internal Server Error.
                throw;
            }
        }

        var updatedLandlord = new LandlordDetails(
            command.BusinessName,
            command.Address,
            profile.Landlord?.CitizenIdNumber,
            profile.Landlord?.CitizenIdIssueDate,
            profile.Landlord?.IdentityFrontBlobName,
            profile.Landlord?.IdentityBackBlobName,
            profile.Landlord?.VerificationStatus ?? KycVerificationStatus.Unverified,
            profile.Landlord?.VerificationNotes,
            profile.Landlord?.VerifiedAt,
            command.BankName,
            command.BankAccountNumber,
            command.BankAccountHolder,
            profile.Landlord?.Rating ?? 5.0,
            profile.Landlord?.ReviewCount ?? 0);

        profile.UpdateLandlordDetails(updatedLandlord, now);

        try
        {
            if (isNewProfile)
            {
                await _repository.AddAsync(profile, cancellationToken);
            }
            else
            {
                await _repository.UpdateAsync(profile, cancellationToken);
            }
        }
        catch
        {
            await CompensatePromotedBlobsAsync(rollbackBlobs);
            throw;
        }

        return await GetMyProfileUseCase.MapToDtoAsync(profile, _kycStorage, cancellationToken);
    }

    private async Task CompensatePromotedBlobsAsync(List<string> blobNames)
    {
        if (_kycStorage is null || blobNames.Count == 0)
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cleanupToken = cts.Token;

        foreach (var blobName in blobNames)
        {
            try
            {
                await _kycStorage.DeleteBlobIfExistsAsync(blobName, cleanupToken);
            }
            catch
            {
                // Best-effort cleanup: deletion failures are logged by the storage service
            }
        }
    }

    private static bool IsInfrastructureException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException or
                TimeoutException or
                HttpRequestException or
                IOException or
                System.Net.Sockets.SocketException)
            {
                return true;
            }

            var typeFullName = current.GetType().FullName ?? string.Empty;
            if (typeFullName.StartsWith("Azure.", StringComparison.Ordinal))
            {
                var statusProp = current.GetType().GetProperty("Status");
                if (statusProp?.GetValue(current) is int status && status == 404)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }
}
