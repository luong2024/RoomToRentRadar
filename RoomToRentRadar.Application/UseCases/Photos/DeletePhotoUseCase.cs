using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

/// <summary>
/// <c>operationId: deletePhoto</c> (BR-MED-03): only the uploader may delete, and only while the photo
/// is still unattached. A foreign or missing photo is a 404 (anti-IDOR, GUARDRAILS §1.5.6), an attached
/// photo a 409 <c>INVALID_STATE_TRANSITION</c>; the files are removed best-effort after the row is gone.
/// </summary>
public sealed class DeletePhotoUseCase : IDeletePhotoUseCase
{
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IPhotoStoragePort _storage;

    public DeletePhotoUseCase(IRoomPhotoRepository photoRepository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(storage);

        _photoRepository = photoRepository;
        _storage = storage;
    }

    public async Task ExecuteAsync(string userId, string photoId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainValidationException("The uploader is required.");
        }

        if (string.IsNullOrWhiteSpace(photoId))
        {
            throw new DomainValidationException("Photo id is required.");
        }

        var attempt = await _photoRepository.DeleteUnattachedAsync(photoId, userId, ct).ConfigureAwait(false);

        switch (attempt.Outcome)
        {
            case PhotoDeleteOutcome.NotFound:
                throw new EntityNotFoundException(ProblemCodes.NotFound, "Photo was not found.");
            case PhotoDeleteOutcome.Attached:
                throw new InvalidStateTransitionException(
                    "Photo is attached to a room and cannot be deleted.",
                    ProblemCodes.InvalidStateTransition);
            default:
                if (attempt.Photo is not null)
                {
                    await PhotoFileCleanup.DeleteAsync(_storage, attempt.Photo).ConfigureAwait(false);
                }

                break;
        }
    }
}
