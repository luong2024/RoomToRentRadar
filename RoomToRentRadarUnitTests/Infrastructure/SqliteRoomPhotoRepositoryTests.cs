using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// US08 / T08.2 / ERD §4.8: SQLite storage of <c>RoomPhotos</c>, ownership scoping and the race-safe
/// attach / remove queries used by createListing and updateListing.
/// </summary>
[TestFixture]
public class SqliteRoomPhotoRepositoryTests
{
    private static readonly SqliteSettings InMemorySettings = new() { ConnectionString = "Data Source=:memory:" };

    private static readonly DateTime FixedNow = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    private SqliteDbContext _dbContext = null!;
    private IRoomPhotoRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = new SqliteDbContext(InMemorySettings);
        InsertLandlord(_dbContext, "landlord-1");
        InsertLandlord(_dbContext, "landlord-2");
        _repository = new SqliteRoomPhotoRepository(_dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task AddAsync_ThenGetOwnedAsync_RoundTripsEveryColumn()
    {
        var photo = NewPhoto("photo-full", ownerId: "landlord-1", kind: PhotoKind.Panorama,
            contentType: RoomPhoto.PngContentType);

        await _repository.AddAsync(photo);
        var stored = await _repository.GetOwnedAsync("photo-full", "landlord-1");

        Assert.That(stored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Id, Is.EqualTo("photo-full"));
            Assert.That(stored.RoomId, Is.Null, "BR-MED-03: ảnh mới lưu chưa gắn phòng.");
            Assert.That(stored.UploadedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(stored.Kind, Is.EqualTo(PhotoKind.Panorama));
            Assert.That(stored.ContentType, Is.EqualTo(RoomPhoto.PngContentType));
            Assert.That(stored.SizeBytes, Is.EqualTo(204_800L));
            Assert.That(stored.Width, Is.EqualTo(1280));
            Assert.That(stored.Height, Is.EqualTo(720));
            Assert.That(stored.OriginalPath, Is.EqualTo("rooms/2026/11/photo-full/original.jpg"));
            Assert.That(stored.MediumPath, Is.EqualTo("rooms/2026/11/photo-full/medium.jpg"));
            Assert.That(stored.ThumbPath, Is.EqualTo("rooms/2026/11/photo-full/thumb.jpg"));
            Assert.That(stored.SortOrder, Is.Zero);
            Assert.That(stored.CreatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(stored.AttachedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task GetOwnedAsync_AnotherUploader_ReturnsNull()
    {
        await _repository.AddAsync(NewPhoto("photo-scoped", ownerId: "landlord-1"));

        var foreign = await _repository.GetOwnedAsync("photo-scoped", "landlord-2");
        var unknown = await _repository.GetOwnedAsync("photo-scoped", "landlord-unknown");

        Assert.Multiple(() =>
        {
            Assert.That(foreign, Is.Null, "GUARDRAILS §1.5.6: ảnh của người khác phải như không tồn tại.");
            Assert.That(unknown, Is.Null);
        });
    }

    [Test]
    public async Task DeleteUnattachedAsync_UnknownOrForeignPhoto_ReturnsNotFound()
    {
        await _repository.AddAsync(NewPhoto("photo-own", ownerId: "landlord-1"));

        var unknown = await _repository.DeleteUnattachedAsync("photo-missing", "landlord-1");
        var foreign = await _repository.DeleteUnattachedAsync("photo-own", "landlord-2");
        var stillThere = await _repository.GetOwnedAsync("photo-own", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Outcome, Is.EqualTo(PhotoDeleteOutcome.NotFound));
            Assert.That(unknown.Photo, Is.Null);
            Assert.That(foreign.Outcome, Is.EqualTo(PhotoDeleteOutcome.NotFound));
            Assert.That(foreign.Photo, Is.Null);
            Assert.That(stillThere, Is.Not.Null, "Người khác không được xoá ảnh.");
        });
    }

    [Test]
    public async Task DeleteUnattachedAsync_AttachedPhoto_ReturnsAttachedAndKeepsRow()
    {
        await AddRoomAsync("room-attached", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-attached", ownerId: "landlord-1", roomId: "room-attached"));

        var attempt = await _repository.DeleteUnattachedAsync("photo-attached", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PhotoDeleteOutcome.Attached));
            Assert.That(attempt.Photo?.Id, Is.EqualTo("photo-attached"));
            Assert.That(attempt.Photo?.RoomId, Is.EqualTo("room-attached"));
        });
        Assert.That(await _repository.GetOwnedAsync("photo-attached", "landlord-1"), Is.Not.Null,
            "Ảnh đã gắn phòng không được xoá qua luồng dọn ảnh.");
    }

    [Test]
    public async Task DeleteUnattachedAsync_UnattachedPhoto_DeletesRowAndReturnsIt()
    {
        await _repository.AddAsync(NewPhoto("photo-delete", ownerId: "landlord-1"));

        var attempt = await _repository.DeleteUnattachedAsync("photo-delete", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PhotoDeleteOutcome.Deleted));
            Assert.That(attempt.Photo?.ThumbPath, Is.EqualTo("rooms/2026/11/photo-delete/thumb.jpg"));
        });
        Assert.That(await _repository.GetOwnedAsync("photo-delete", "landlord-1"), Is.Null);
    }

    [Test]
    public async Task ListExpiredUnattachedAsync_ReturnsOnlyUnattachedRowsOlderThanCutoff()
    {
        await AddRoomAsync("room-expiry", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-old-a", createdAtUtc: FixedNow.AddHours(-30)));
        await _repository.AddAsync(NewPhoto("photo-old-b", createdAtUtc: FixedNow.AddHours(-26)));
        await _repository.AddAsync(NewPhoto("photo-fresh", createdAtUtc: FixedNow.AddHours(-1)));
        await _repository.AddAsync(NewPhoto("photo-old-attached", roomId: "room-expiry",
            createdAtUtc: FixedNow.AddHours(-48)));

        var expired = await _repository.ListExpiredUnattachedAsync(FixedNow.AddHours(-24));

        Assert.That(expired.Select(photo => photo.Id), Is.EqualTo(new[] { "photo-old-a", "photo-old-b" }),
            "BR-MED-03: chỉ ảnh chưa gắn và quá 24h, sắp theo CreatedAtUtc.");
    }

    [Test]
    public async Task ListByRoomAsync_ReturnsAttachedPhotosInSortOrder()
    {
        await AddRoomAsync("room-gallery", "landlord-1");
        await AddRoomAsync("room-other", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-second", roomId: "room-gallery", sortOrder: 1));
        await _repository.AddAsync(NewPhoto("photo-cover", roomId: "room-gallery", sortOrder: 0));
        await _repository.AddAsync(NewPhoto("photo-other-room", roomId: "room-other", sortOrder: 0));
        await _repository.AddAsync(NewPhoto("photo-unattached"));

        var photos = await _repository.ListByRoomAsync("room-gallery");

        Assert.That(photos.Select(photo => photo.Id), Is.EqualTo(new[] { "photo-cover", "photo-second" }));
    }

    [Test]
    public async Task ListOwnedByIdsAsync_ScopesByUploaderAndEmptyRequestSkipsQuery()
    {
        await _repository.AddAsync(NewPhoto("photo-a", ownerId: "landlord-1"));
        await _repository.AddAsync(NewPhoto("photo-b", ownerId: "landlord-1"));
        await _repository.AddAsync(NewPhoto("photo-foreign", ownerId: "landlord-2"));

        var owned = await _repository.ListOwnedByIdsAsync(
            "landlord-1", new[] { "photo-a", "photo-b", "photo-foreign" });
        var empty = await _repository.ListOwnedByIdsAsync("landlord-1", Array.Empty<string>());

        Assert.Multiple(() =>
        {
            Assert.That(owned.Select(photo => photo.Id), Is.EquivalentTo(new[] { "photo-a", "photo-b" }));
            Assert.That(empty, Is.Empty);
        });
    }

    [Test]
    public async Task AttachAsync_AssignsSortOrderAndOneAttachedTimestamp()
    {
        await AddRoomAsync("room-attach", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-1", ownerId: "landlord-1"));
        await _repository.AddAsync(NewPhoto("photo-2", ownerId: "landlord-1"));
        await _repository.AddAsync(NewPhoto("photo-3", ownerId: "landlord-1"));

        await _repository.AttachAsync("landlord-1", "room-attach", new[] { "photo-3", "photo-1", "photo-2" });

        var photos = await _repository.ListByRoomAsync("room-attach");
        Assert.Multiple(() =>
        {
            Assert.That(photos.Select(photo => photo.Id), Is.EqualTo(new[] { "photo-3", "photo-1", "photo-2" }));
            Assert.That(photos.Select(photo => photo.SortOrder), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(photos.Select(photo => photo.AttachedAtUtc), Is.All.Not.Null);
            Assert.That(photos.Select(photo => photo.AttachedAtUtc).Distinct().Count(), Is.EqualTo(1),
                "Một timestamp UTC cho cả lần attach.");
        });
    }

    [Test]
    public async Task AttachAsync_PhotoOfAnotherUploader_ThrowsValidationFailed()
    {
        await AddRoomAsync("room-steal", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-victim", ownerId: "landlord-2"));

        await Assert.ThatAsync(
            async () => await _repository.AttachAsync("landlord-1", "room-steal", new[] { "photo-victim" }),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));

        Assert.That((await _repository.GetOwnedAsync("photo-victim", "landlord-2"))!.RoomId, Is.Null,
            "Ảnh của người khác phải giữ nguyên trạng thái chưa gắn.");
    }

    [Test]
    public async Task AttachAsync_PhotoAlreadyAttachedToAnotherRoom_ThrowsValidationFailed()
    {
        await AddRoomAsync("room-first", "landlord-1");
        await AddRoomAsync("room-second", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-taken", ownerId: "landlord-1", roomId: "room-first"));

        await Assert.ThatAsync(
            async () => await _repository.AttachAsync("landlord-1", "room-second", new[] { "photo-taken" }),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));

        var stored = await _repository.GetOwnedAsync("photo-taken", "landlord-1");
        Assert.That(stored!.RoomId, Is.EqualTo("room-first"), "Không được cướp ảnh của phòng khác.");
    }

    [Test]
    public async Task RemoveNotInListAsync_DeletesRemovedRowsAndReturnsThemInSortOrder()
    {
        await AddRoomAsync("room-shrink", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-keep", ownerId: "landlord-1", roomId: "room-shrink", sortOrder: 0));
        await _repository.AddAsync(NewPhoto("photo-drop-a", ownerId: "landlord-1", roomId: "room-shrink", sortOrder: 1));
        await _repository.AddAsync(NewPhoto("photo-drop-b", ownerId: "landlord-1", roomId: "room-shrink", sortOrder: 2));

        var removed = await _repository.RemoveNotInListAsync("room-shrink", new[] { "photo-keep" });

        Assert.Multiple(() =>
        {
            Assert.That(removed.Select(photo => photo.Id), Is.EqualTo(new[] { "photo-drop-a", "photo-drop-b" }));
            Assert.That(removed.Select(photo => photo.SortOrder), Is.EqualTo(new[] { 1, 2 }));
        });
        Assert.That((await _repository.ListByRoomAsync("room-shrink")).Select(photo => photo.Id),
            Is.EqualTo(new[] { "photo-keep" }));
    }

    [Test]
    public async Task RemoveNotInListAsync_EmptyKeepList_RemovesEveryPhotoOfTheRoom()
    {
        await AddRoomAsync("room-empty", "landlord-1");
        await AddRoomAsync("room-other", "landlord-1");
        await _repository.AddAsync(NewPhoto("photo-only", ownerId: "landlord-1", roomId: "room-empty"));
        await _repository.AddAsync(NewPhoto("photo-elsewhere", ownerId: "landlord-1", roomId: "room-other"));

        var removed = await _repository.RemoveNotInListAsync("room-empty", Array.Empty<string>());
        var remaining = await _repository.ListByRoomAsync("room-empty");

        Assert.Multiple(() =>
        {
            Assert.That(removed.Select(photo => photo.Id), Is.EqualTo(new[] { "photo-only" }));
            Assert.That(remaining, Is.Empty);
        });
    }

    [Test]
    public async Task AddAsync_PhotoForMissingUser_FailsWithForeignKeyViolation()
    {
        var photo = NewPhoto("photo-orphan", ownerId: "user-khong-ton-tai");

        var exception = Assert.ThrowsAsync<SqliteException>(async () => await _repository.AddAsync(photo));

        Assert.That(exception!.SqliteErrorCode, Is.EqualTo(19), "UploadedByUserId phải tham chiếu Users.");
    }

    // ----------------------------------------------------------------- fixtures

    private static RoomPhoto NewPhoto(
        string id,
        string ownerId = "landlord-1",
        string? roomId = null,
        PhotoKind kind = PhotoKind.Standard,
        string contentType = RoomPhoto.JpegContentType,
        int sortOrder = 0,
        DateTime? createdAtUtc = null) =>
        RoomPhoto.Reconstitute(
            id: id,
            roomId: roomId,
            uploadedByUserId: ownerId,
            kind: kind,
            contentType: contentType,
            sizeBytes: 204_800,
            width: 1280,
            height: 720,
            originalPath: $"rooms/2026/11/{id}/original.jpg",
            mediumPath: $"rooms/2026/11/{id}/medium.jpg",
            thumbPath: $"rooms/2026/11/{id}/thumb.jpg",
            sortOrder: sortOrder,
            createdAtUtc: createdAtUtc ?? FixedNow,
            attachedAtUtc: null);

    private async Task AddRoomAsync(string roomId, string landlordId)
    {
        var room = Room.Reconstitute(
            id: roomId,
            landlordId: landlordId,
            title: "Phòng cho kiểm thử ảnh",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 20.0,
            addressLine: "Tân Thịnh",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            status: ListingStatus.PendingReview,
            createdAtUtc: FixedNow,
            lastUpdatedAtUtc: FixedNow,
            version: 1);

        await new SqliteRoomRepository(_dbContext).AddAsync(room);
    }

    private static void InsertLandlord(SqliteDbContext dbContext, string userId)
    {
        var connection = dbContext.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Id, Email, NormalizedEmail, FullName, Role, AuthProvider, IsVerified, FailedLoginCount, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@id, @email, @normalizedEmail, 'Test Landlord', 'Landlord', 'Local', 1, 0, '2026-11-15T08:00:00.0000000Z', '2026-11-15T08:00:00.0000000Z');
            """;
        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@email", userId + "@example.com");
        command.Parameters.AddWithValue("@normalizedEmail", userId.ToUpperInvariant() + "@EXAMPLE.COM");
        command.ExecuteNonQuery();
    }
}
