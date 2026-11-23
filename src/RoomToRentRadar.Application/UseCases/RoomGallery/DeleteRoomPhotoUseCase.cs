using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

public sealed class DeleteRoomPhotoUseCase : IDeleteRoomPhotoUseCase
{
    public const int DefaultMaxStorageDeleteAttempts = 3;
    private static readonly TimeSpan DefaultStorageDeleteRetryDelay = TimeSpan.FromMilliseconds(150);

    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IRoomPhotoStorageService _storageService;
    private readonly int _maxStorageDeleteAttempts;
    private readonly TimeSpan _storageDeleteRetryDelay;

    public DeleteRoomPhotoUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IRoomPhotoStorageService storageService)
        : this(
            roomRepository,
            photoRepository,
            storageService,
            DefaultMaxStorageDeleteAttempts,
            DefaultStorageDeleteRetryDelay)
    {
    }

    internal DeleteRoomPhotoUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IRoomPhotoStorageService storageService,
        int maxStorageDeleteAttempts,
        TimeSpan storageDeleteRetryDelay)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _maxStorageDeleteAttempts = Math.Max(1, maxStorageDeleteAttempts);
        _storageDeleteRetryDelay = storageDeleteRetryDelay < TimeSpan.Zero ? TimeSpan.Zero : storageDeleteRetryDelay;
    }

    public async Task ExecuteAsync(
        string roomId,
        string photoId,
        Guid actorUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new InvalidRoomPhotoException("Room ID is required.");
        }

        if (string.IsNullOrWhiteSpace(photoId))
        {
            throw new InvalidRoomPhotoException("Photo ID is required.");
        }

        var normalizedRoomId = roomId.Trim();
        var normalizedPhotoId = photoId.Trim();

        var room = await _roomRepository.GetByIdAsync(normalizedRoomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Room '{normalizedRoomId}' was not found.");
        }

        RoomGalleryAuthorization.EnsureCanManageGallery(room, actorUserId, isAdmin);

        // Phase 1 (Atomic DB): Mark photo as pending deletion (IsDeleted = true), hide from gallery,
        // free quota slot, and promote the next cover photo in one transactional batch.
        var targetPhoto = await _photoRepository.MarkPendingDeletionAsync(normalizedRoomId, normalizedPhotoId, cancellationToken);
        if (targetPhoto is null)
        {
            throw new RoomPhotoNotFoundException($"Photo '{normalizedPhotoId}' was not found in room '{normalizedRoomId}'.");
        }

        // Phase 2 (Storage + Hard-delete): Delete R2 objects with retry; if storage fails or is cancelled,
        // the photo stays hidden in pending-deletion state and can be cleaned up safely later without broken URLs.
        await DeleteStorageWithRetryAsync(targetPhoto.ObjectKey, targetPhoto.ThumbnailObjectKey, cancellationToken);
        await _photoRepository.DeleteAsync(normalizedRoomId, normalizedPhotoId, CancellationToken.None);
    }

    public async Task<int> CleanupPendingDeletionsAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return 0;
        }

        var normalizedRoomId = roomId.Trim();
        var pending = await _photoRepository.GetPendingDeletionAsync(normalizedRoomId, cancellationToken);
        var cleanedCount = 0;

        foreach (var photo in pending)
        {
            await DeleteStorageWithRetryAsync(photo.ObjectKey, photo.ThumbnailObjectKey, cancellationToken);
            await _photoRepository.DeleteAsync(normalizedRoomId, photo.Id, CancellationToken.None);
            cleanedCount++;
        }

        return cleanedCount;
    }

    private async Task DeleteStorageWithRetryAsync(
        string objectKey,
        string thumbnailObjectKey,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _storageService.DeleteAsync(objectKey, thumbnailObjectKey, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch when (attempt < _maxStorageDeleteAttempts)
            {
                if (_storageDeleteRetryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_storageDeleteRetryDelay, cancellationToken);
                }
            }
        }
    }
}
