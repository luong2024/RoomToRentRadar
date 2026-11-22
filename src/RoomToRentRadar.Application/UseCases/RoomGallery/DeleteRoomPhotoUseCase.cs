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

        var targetPhoto = await _photoRepository.GetByIdAsync(normalizedRoomId, normalizedPhotoId, cancellationToken);
        if (targetPhoto is null)
        {
            throw new RoomPhotoNotFoundException($"Photo '{normalizedPhotoId}' was not found in room '{normalizedRoomId}'.");
        }

        // Delete storage objects first (idempotent + retryable) so metadata remains intact
        // and the delete operation can be safely retried if storage deletion fails.
        await DeleteStorageWithRetryAsync(targetPhoto.ObjectKey, targetPhoto.ThumbnailObjectKey, cancellationToken);
        await _photoRepository.DeleteAsync(normalizedRoomId, normalizedPhotoId, cancellationToken);

        if (targetPhoto.IsCover)
        {
            var remaining = await _photoRepository.GetByRoomIdAsync(normalizedRoomId, cancellationToken);
            var nextCover = remaining
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.UploadedAtUtc)
                .FirstOrDefault();

            if (nextCover is not null && !nextCover.IsCover)
            {
                nextCover.SetAsCover(true);
                await _photoRepository.UpdateAsync(nextCover, cancellationToken);
            }
        }
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
