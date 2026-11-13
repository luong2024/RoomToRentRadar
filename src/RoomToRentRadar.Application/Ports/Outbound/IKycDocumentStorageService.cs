using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound port for secure cloud/blob storage of private KYC identity documents.
/// Protects unauthenticated and unauthorized access by generating cryptographically-signed,
/// time-limited SAS (Shared Access Signature) read and upload URLs with restricted permissions.
/// Also validates document source URLs against authorized storage containers and accounts,
/// and validates uploaded documents for ownership, existence, content type, and size.
/// </summary>
public interface IKycDocumentStorageService
{
    Task<string?> GeneratePresignedReadUrlAsync(
        string? rawBlobPath,
        TimeSpan validDuration,
        CancellationToken cancellationToken = default);

    Task<KycUploadUrlResponse> GeneratePresignedUploadUrlAsync(
        Guid userId,
        string documentSide,
        string contentType,
        CancellationToken cancellationToken = default);

    Task ValidateUploadedDocumentAsync(
        Guid userId,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a client-uploaded staging blob, then moves it to the server-owned verified area.
    /// The returned blob name is the only name that may be persisted on a profile.
    /// </summary>
    Task<string> ValidateAndPromoteUploadedDocumentAsync(
        Guid userId,
        string documentSide,
        string blobName,
        CancellationToken cancellationToken = default);

    void ValidateDocumentUrl(string? rawBlobPath);

    bool IsAuthorizedDocumentUrl(string? rawBlobPath);

    /// <summary>
    /// Deletes a blob if it exists, used for compensating transactions or cleanup.
    /// </summary>
    Task DeleteBlobIfExistsAsync(string? blobName, CancellationToken cancellationToken = default);
}
