using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Datastore.Sqlite.Transactions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Infrastructure.Adapters.Storage;

namespace RoomToRentRadarUnitTests.Infrastructure;

// BR-MED-03: the hourly cleanup removes expired metadata and its stored files.
[TestFixture]
public class CleanupExpiredPhotosIntegrationTests
{
    [Test]
    public async Task ExecuteAsync_WithRealSqliteAndDisk_DeletesExpiredMetadataAndFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "rtrr-cleanup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
            var repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
            var storage = new LocalDiskPhotoStorageAdapter(root, "http://localhost");
            var expired = CreateExpiredPhoto("expired-1");
            await repository.AddAsync(expired);

            var originalPath = Path.Combine(
                root,
                expired.OriginalPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            await File.WriteAllTextAsync(originalPath, "image-bytes");

            var useCase = new CleanupExpiredPhotosUseCase(repository, storage);
            var deleted = await useCase.ExecuteAsync();

            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.EqualTo(1));
                Assert.That(File.Exists(originalPath), Is.False);
            });
            Assert.That(await repository.GetOwnedAsync(expired.Id, expired.UploadedByUserId), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RoomPhoto CreateExpiredPhoto(string id) => new(
        uploadedByUserId: "landlord-1",
        kind: PhotoKind.Standard,
        contentType: "image/jpeg",
        sizeBytes: 1234,
        width: 800,
        height: 600,
        originalPath: $"rooms/2026/11/{id}/original.jpg",
        mediumPath: $"rooms/2026/11/{id}/medium.jpg",
        thumbPath: $"rooms/2026/11/{id}/thumb.jpg",
        id: id,
        createdAtUtc: DateTime.UtcNow.AddHours(-25));
}
