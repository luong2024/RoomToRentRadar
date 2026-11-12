using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

// deletePhoto — BR-MED-03.
public sealed class DeletePhotoUseCase : IDeletePhotoUseCase
{
    private readonly IRoomPhotoRepository _repository;
    private readonly IPhotoStoragePort _storage;

    public DeletePhotoUseCase(IRoomPhotoRepository repository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);

        _repository = repository;
        _storage = storage;
    }

    public async Task<DeletePhotoResult> ExecuteAsync(string photoId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(photoId))
        {
            throw new DomainValidationException("Photo id is required.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainValidationException("User id is required.");
        }

        // Anti-IDOR: the repository only returns photos uploaded by this user, so another
        // user's photo is indistinguishable from a missing one.
        var photo = await _repository.GetOwnedAsync(photoId, userId, ct);
        if (photo is null)
        {
            return DeletePhotoResult.NotFound;
        }

        if (photo.RoomId is not null)
        {
            return DeletePhotoResult.Attached;
        }

        // The DELETE and its miss-reason lookup happen atomically in the repository, so a room
        // attached between the read above and the delete still yields 409 (anti-TOCTOU).
        var outcome = await _repository.DeleteUnattachedAsync(photoId, userId, ct);
        if (outcome == RoomPhotoDeleteOutcome.NotFound)
        {
            return DeletePhotoResult.NotFound;
        }

        if (outcome == RoomPhotoDeleteOutcome.Attached)
        {
            return DeletePhotoResult.Attached;
        }

        // Metadata is gone, so still try to remove every stored object; storage failures
        // are swallowed because the delete already succeeded from the user's point of view.
        var paths = new[] { photo.OriginalPath, photo.MediumPath, photo.ThumbPath }
            .Distinct(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            try
            {
                await _storage.DeleteAsync(path, CancellationToken.None);
            }
            catch
            {
                // Best effort only.
            }
        }

        return DeletePhotoResult.Deleted;
    }
}
