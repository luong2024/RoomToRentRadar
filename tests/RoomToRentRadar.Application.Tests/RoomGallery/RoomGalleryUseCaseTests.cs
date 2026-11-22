using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomGallery;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.Tests.RoomGallery;

[TestFixture]
public class RoomGalleryUseCaseTests
{
    private FakeRoomRepository _roomRepo = null!;
    private FakeRoomPhotoRepository _photoRepo = null!;
    private FakeCompressionService _compressionService = null!;
    private FakePhotoStorageService _storageService = null!;
    private Guid _ownerLandlordId;

    [SetUp]
    public void SetUp()
    {
        _ownerLandlordId = Guid.NewGuid();
        _roomRepo = new FakeRoomRepository();
        _roomRepo.Seed(new Room
        {
            Id = "room-001",
            Title = "ICTU Room",
            Status = RoomStatus.Available,
            IsVerified = true,
            LandlordId = _ownerLandlordId
        });
        _photoRepo = new FakeRoomPhotoRepository();
        _compressionService = new FakeCompressionService();
        _storageService = new FakePhotoStorageService();
    }

    [Test]
    public async Task GetRoomGallery_AC4_WhenRoomHasNoPhotos_ReturnsEmptyGalleryWithHasPhotosFalse()
    {
        var useCase = new GetRoomGalleryUseCase(_roomRepo, _photoRepo);

        var result = await useCase.ExecuteAsync("room-001");

        Assert.Multiple(() =>
        {
            Assert.That(result.RoomId, Is.EqualTo("room-001"));
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.HasPhotos, Is.False);
            Assert.That(result.Has360Photos, Is.False);
            Assert.That(result.Photos, Is.Empty);
        });
    }

    [Test]
    public void GetRoomGallery_WhenRoomDoesNotExist_ThrowsNotFoundException()
    {
        var useCase = new GetRoomGalleryUseCase(_roomRepo, _photoRepo);

        Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync("non-existent"));
    }

    [Test]
    public async Task UploadRoomPhoto_AC1_SupportsAtLeast8PhotosAndEnforcesMaxQuotaOf12()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var uploaderId = _ownerLandlordId;

        for (var i = 0; i < RoomPhoto.MaxPhotosPerRoom; i++)
        {
            var dto = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
                RoomId: "room-001",
                UploadedByUserId: uploaderId,
                FileName: $"photo-{i}.jpg",
                ContentType: "image/jpeg",
                FileBytes: [0xFF, 0xD8, 0xFF, 0xE0],
                Is360: i == 0));

            Assert.That(dto.DisplayOrder, Is.EqualTo(i));
        }

        Assert.ThrowsAsync<RoomPhotoQuotaExceededException>(() => uploadUseCase.ExecuteAsync(
            new UploadRoomPhotoCommand(
                RoomId: "room-001",
                UploadedByUserId: uploaderId,
                FileName: "overflow.jpg",
                ContentType: "image/jpeg",
                FileBytes: [0xFF, 0xD8, 0xFF, 0xE0])));

        var galleryUseCase = new GetRoomGalleryUseCase(_roomRepo, _photoRepo);
        var gallery = await galleryUseCase.ExecuteAsync("room-001");

        Assert.Multiple(() =>
        {
            Assert.That(gallery.TotalCount, Is.EqualTo(RoomPhoto.MaxPhotosPerRoom));
            Assert.That(gallery.TotalCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(gallery.HasPhotos, Is.True);
            Assert.That(gallery.Has360Photos, Is.True);
            Assert.That(gallery.Photos[0].IsCover, Is.True);
        });
    }

    [Test]
    public void UploadRoomPhoto_WhenPersistenceFails_RollsBackUploadedStorageObjects()
    {
        _photoRepo.FailNextAdd = true;
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);

        Assert.ThrowsAsync<InvalidOperationException>(() => uploadUseCase.ExecuteAsync(
            new UploadRoomPhotoCommand(
                RoomId: "room-001",
                UploadedByUserId: _ownerLandlordId,
                FileName: "room.jpg",
                ContentType: "image/jpeg",
                FileBytes: [0xFF, 0xD8, 0xFF, 0xE0])));

        Assert.That(_storageService.DeletedKeys, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UploadRoomPhoto_WhenUploadingNewCoverAndAddFails_PreservesPreviousCoverAndRollsBackStorage()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);

        var existingCover = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            RoomId: "room-001",
            UploadedByUserId: _ownerLandlordId,
            FileName: "existing-cover.jpg",
            ContentType: "image/jpeg",
            FileBytes: [0xFF, 0xD8, 0xFF, 0xE0],
            IsCover: true));

        _photoRepo.FailNextAdd = true;

        Assert.ThrowsAsync<InvalidOperationException>(() => uploadUseCase.ExecuteAsync(
            new UploadRoomPhotoCommand(
                RoomId: "room-001",
                UploadedByUserId: _ownerLandlordId,
                FileName: "new-cover.jpg",
                ContentType: "image/jpeg",
                FileBytes: [0xFF, 0xD8, 0xFF, 0xE0],
                IsCover: true)));

        var photos = await _photoRepo.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(1));
            Assert.That(photos[0].Id, Is.EqualTo(existingCover.Id));
            Assert.That(photos[0].IsCover, Is.True);
            Assert.That(_storageService.DeletedKeys, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task UploadRoomPhoto_WhenDemotingPreviousCoverFails_RollsBackNewPhotoAndStorageAndRestoresCover()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);

        var existingCover = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            RoomId: "room-001",
            UploadedByUserId: _ownerLandlordId,
            FileName: "existing-cover.jpg",
            ContentType: "image/jpeg",
            FileBytes: [0xFF, 0xD8, 0xFF, 0xE0],
            IsCover: true));

        _photoRepo.FailNextUpdate = true;

        Assert.ThrowsAsync<InvalidOperationException>(() => uploadUseCase.ExecuteAsync(
            new UploadRoomPhotoCommand(
                RoomId: "room-001",
                UploadedByUserId: _ownerLandlordId,
                FileName: "new-cover.jpg",
                ContentType: "image/jpeg",
                FileBytes: [0xFF, 0xD8, 0xFF, 0xE0],
                IsCover: true)));

        var photos = await _photoRepo.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(1));
            Assert.That(photos[0].Id, Is.EqualTo(existingCover.Id));
            Assert.That(photos[0].IsCover, Is.True);
            Assert.That(_storageService.DeletedKeys, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task DeleteRoomPhoto_WhenCoverDeleted_PromotesNextRemainingPhotoToCover()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var deleteUseCase = new DeleteRoomPhotoUseCase(_roomRepo, _photoRepo, _storageService);

        var first = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "1.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));
        var second = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "2.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));

        await deleteUseCase.ExecuteAsync("room-001", first.Id, _ownerLandlordId);

        var remaining = await _photoRepo.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(remaining, Has.Count.EqualTo(1));
            Assert.That(remaining[0].Id, Is.EqualTo(second.Id));
            Assert.That(remaining[0].IsCover, Is.True);
        });
    }

    [Test]
    public async Task DeleteRoomPhoto_WhenStorageDeleteFailsPersistently_PreservesMetadataAndAllowsRetry()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var deleteUseCase = new DeleteRoomPhotoUseCase(_roomRepo, _photoRepo, _storageService);

        var photo = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "1.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));

        _storageService.FailDeleteCount = DeleteRoomPhotoUseCase.DefaultMaxStorageDeleteAttempts;

        Assert.ThrowsAsync<IOException>(() => deleteUseCase.ExecuteAsync("room-001", photo.Id, _ownerLandlordId));

        // Metadata must still exist because storage deletion failed.
        var stillPresent = await _photoRepo.GetByIdAsync("room-001", photo.Id);
        Assert.That(stillPresent, Is.Not.Null);
        Assert.That(_storageService.DeleteAttemptCount, Is.EqualTo(DeleteRoomPhotoUseCase.DefaultMaxStorageDeleteAttempts));

        // Retrying after storage recovers deletes both storage and metadata cleanly.
        await deleteUseCase.ExecuteAsync("room-001", photo.Id, _ownerLandlordId);
        var afterRetry = await _photoRepo.GetByIdAsync("room-001", photo.Id);
        Assert.That(afterRetry, Is.Null);
        Assert.That(_storageService.DeletedKeys, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DeleteRoomPhoto_WhenStorageDeleteFailsTransiently_RetriesAndSucceeds()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var deleteUseCase = new DeleteRoomPhotoUseCase(_roomRepo, _photoRepo, _storageService);

        var photo = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "1.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));

        _storageService.FailDeleteCount = 2;

        await deleteUseCase.ExecuteAsync("room-001", photo.Id, _ownerLandlordId);

        var remaining = await _photoRepo.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(remaining, Is.Empty);
            Assert.That(_storageService.DeleteAttemptCount, Is.EqualTo(3));
            Assert.That(_storageService.DeletedKeys, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ReorderRoomPhotos_UpdatesDisplayOrderAndCoverPhoto()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var reorderUseCase = new ReorderRoomPhotosUseCase(_roomRepo, _photoRepo);

        var p1 = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "1.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));
        var p2 = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "2.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));

        var updated = await reorderUseCase.ExecuteAsync(new ReorderRoomPhotosCommand(
            RoomId: "room-001",
            ActorUserId: _ownerLandlordId,
            OrderedPhotoIds: [p2.Id, p1.Id],
            CoverPhotoId: p2.Id));

        Assert.Multiple(() =>
        {
            Assert.That(updated.Photos[0].Id, Is.EqualTo(p2.Id));
            Assert.That(updated.Photos[0].DisplayOrder, Is.EqualTo(0));
            Assert.That(updated.Photos[0].IsCover, Is.True);
            Assert.That(updated.Photos[1].Id, Is.EqualTo(p1.Id));
            Assert.That(updated.Photos[1].DisplayOrder, Is.EqualTo(1));
            Assert.That(updated.Photos[1].IsCover, Is.False);
        });
    }

    [Test]
    public async Task WriteUseCases_WhenLandlordDoesNotOwnRoom_ThrowsRoomGalleryAccessDeniedException()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var deleteUseCase = new DeleteRoomPhotoUseCase(_roomRepo, _photoRepo, _storageService);
        var reorderUseCase = new ReorderRoomPhotosUseCase(_roomRepo, _photoRepo);

        var existing = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", _ownerLandlordId, "owner.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]));

        var otherLandlordId = Guid.NewGuid();

        Assert.ThrowsAsync<RoomGalleryAccessDeniedException>(() => uploadUseCase.ExecuteAsync(
            new UploadRoomPhotoCommand("room-001", otherLandlordId, "intruder.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF])));
        Assert.ThrowsAsync<RoomGalleryAccessDeniedException>(() => deleteUseCase.ExecuteAsync(
            "room-001", existing.Id, otherLandlordId));
        Assert.ThrowsAsync<RoomGalleryAccessDeniedException>(() => reorderUseCase.ExecuteAsync(
            new ReorderRoomPhotosCommand("room-001", otherLandlordId, [existing.Id], existing.Id)));
    }

    [Test]
    public async Task WriteUseCases_WhenActorIsAdmin_AllowsManagingAnyRoomGallery()
    {
        var uploadUseCase = new UploadRoomPhotoUseCase(_roomRepo, _photoRepo, _compressionService, _storageService);
        var deleteUseCase = new DeleteRoomPhotoUseCase(_roomRepo, _photoRepo, _storageService);
        var reorderUseCase = new ReorderRoomPhotosUseCase(_roomRepo, _photoRepo);

        var adminId = Guid.NewGuid();

        var p1 = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", adminId, "admin1.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF], IsAdmin: true));
        var p2 = await uploadUseCase.ExecuteAsync(new UploadRoomPhotoCommand(
            "room-001", adminId, "admin2.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF], IsAdmin: true));

        var reordered = await reorderUseCase.ExecuteAsync(new ReorderRoomPhotosCommand(
            "room-001", adminId, [p2.Id, p1.Id], p2.Id, IsAdmin: true));
        Assert.That(reordered.Photos[0].Id, Is.EqualTo(p2.Id));

        await deleteUseCase.ExecuteAsync("room-001", p1.Id, adminId, isAdmin: true);
        var remaining = await _photoRepo.GetByRoomIdAsync("room-001");
        Assert.That(remaining, Has.Count.EqualTo(1));
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(Room room) => _rooms[room.Id] = room;

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rooms.TryGetValue(id, out var room) ? room : null);

        public Task<IReadOnlyList<Room>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>(ids.Where(_rooms.ContainsKey).Select(id => _rooms[id]).ToList());

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>([]);

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<Room>, int)>(([], 0));
    }

    private sealed class FakeRoomPhotoRepository : IRoomPhotoRepository
    {
        private readonly List<RoomPhoto> _photos = [];
        public bool FailNextAdd { get; set; }
        public bool FailNextUpdate { get; set; }

        public Task<IReadOnlyList<RoomPhoto>> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoomPhoto>>(_photos.Where(p => p.RoomId == roomId).OrderBy(p => p.DisplayOrder).ToList());

        public Task<RoomPhoto?> GetByIdAsync(string roomId, string photoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_photos.FirstOrDefault(p => p.RoomId == roomId && p.Id == photoId));

        public Task<int> CountByRoomIdAsync(string roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_photos.Count(p => p.RoomId == roomId));

        public Task AddAsync(RoomPhoto photo, CancellationToken cancellationToken = default)
        {
            if (FailNextAdd)
            {
                FailNextAdd = false;
                throw new InvalidOperationException("Simulated DB write failure.");
            }

            _photos.Add(photo);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RoomPhoto photo, CancellationToken cancellationToken = default)
        {
            if (FailNextUpdate)
            {
                FailNextUpdate = false;
                throw new InvalidOperationException("Simulated DB update failure.");
            }

            var idx = _photos.FindIndex(p => p.Id == photo.Id);
            if (idx >= 0) _photos[idx] = photo;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string roomId, string photoId, CancellationToken cancellationToken = default)
        {
            _photos.RemoveAll(p => p.RoomId == roomId && p.Id == photoId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCompressionService : IImageCompressionService
    {
        public Task<ProcessedImageResult> ProcessAsync(
            byte[] rawBytes,
            string declaredContentType,
            bool is360,
            CancellationToken cancellationToken = default)
        {
            var width = is360 ? 4096 : 1920;
            var height = is360 ? 2048 : 1080;
            return Task.FromResult(new ProcessedImageResult(
                CompressedBytes: [0xFF, 0xD8, 0xFF, 0xD9],
                ThumbnailBytes: [0xFF, 0xD8, 0xFF, 0xD9],
                ContentType: "image/jpeg",
                FileExtension: ".jpg",
                Width: width,
                Height: height));
        }
    }

    private sealed class FakePhotoStorageService : IRoomPhotoStorageService
    {
        public List<string> DeletedKeys { get; } = [];
        public int FailDeleteCount { get; set; }
        public int DeleteAttemptCount { get; private set; }

        public Task<StoredPhotoResult> UploadAsync(
            string roomId,
            string photoId,
            ProcessedImageResult processedImage,
            CancellationToken cancellationToken = default)
        {
            var key = $"rooms/{roomId}/{photoId}.jpg";
            var thumbKey = $"rooms/{roomId}/{photoId}_thumb.jpg";
            return Task.FromResult(new StoredPhotoResult(
                Url: $"https://cdn.test/{key}",
                ThumbnailUrl: $"https://cdn.test/{thumbKey}",
                ObjectKey: key,
                ThumbnailObjectKey: thumbKey));
        }

        public Task DeleteAsync(string objectKey, string thumbnailObjectKey, CancellationToken cancellationToken = default)
        {
            DeleteAttemptCount++;
            if (FailDeleteCount > 0)
            {
                FailDeleteCount--;
                throw new IOException("Simulated R2 delete failure.");
            }

            DeletedKeys.Add(objectKey);
            DeletedKeys.Add(thumbnailObjectKey);
            return Task.CompletedTask;
        }
    }
}
