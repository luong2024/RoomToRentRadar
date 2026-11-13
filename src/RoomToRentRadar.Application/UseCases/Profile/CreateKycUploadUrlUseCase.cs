using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class CreateKycUploadUrlUseCase : ICreateKycUploadUrlUseCase
{
    private readonly IKycDocumentStorageService _kycStorage;

    public CreateKycUploadUrlUseCase(IKycDocumentStorageService kycStorage)
    {
        _kycStorage = kycStorage ?? throw new ArgumentNullException(nameof(kycStorage));
    }

    public async Task<KycUploadUrlResponse> ExecuteAsync(
        Guid userId,
        CreateKycUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DocumentSide))
        {
            throw new KycOperationInvalidException("Document side (e.g., 'front' or 'back') is required.");
        }

        var side = request.DocumentSide.Trim().ToLowerInvariant();
        if (side != "front" && side != "back")
        {
            throw new KycOperationInvalidException("Document side must be either 'front' or 'back'.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new KycOperationInvalidException("Content type is required.");
        }

        var contentType = request.ContentType.Trim().ToLowerInvariant();
        if (contentType != "image/jpeg" && contentType != "image/jpg" && contentType != "image/png")
        {
            throw new KycOperationInvalidException("Only JPEG and PNG image content types are supported.");
        }

        return await _kycStorage.GeneratePresignedUploadUrlAsync(
            userId,
            side,
            contentType,
            cancellationToken);
    }
}
