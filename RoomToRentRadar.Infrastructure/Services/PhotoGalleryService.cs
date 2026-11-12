using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Data;

namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Implementation of IPhotoGalleryService for US04 / T04.1 (pure .NET, no MAUI dependency).
    /// - Files are stored under {storageRoot}/room-photos/{roomId}/{GUID}.jpg (+ {GUID}_thumb.jpg).
    ///   The MAUI app passes FileSystem.AppDataDirectory as storageRoot; tests pass a temp folder.
    /// - Decoding / JPEG re-encoding / thumbnails are delegated to <see cref="IPhotoImageProcessor"/>.
    /// - The database only stores keys: PhotosJson (JSON array) and Panorama360Url, via RentalDatabaseService.
    /// - Seed values ("dotnet_bot.png", https URLs) are returned as-is; they have no separate thumbnail.
    /// </summary>
    public class PhotoGalleryService : IPhotoGalleryService
    {
        private const string PhotoFolder = "room-photos";
        private const string ThumbnailSuffix = "_thumb";
        private const string StoredExtension = ".jpg";

        private readonly RentalDatabaseService _database;
        private readonly IAuthService _authService;
        private readonly IPhotoImageProcessor _imageProcessor;
        private readonly ILogger<PhotoGalleryService> _logger;
        private readonly string _storageRoot;

        // Serializes read-modify-write of PhotosJson so two uploads cannot overwrite each other.
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public PhotoGalleryService(
            RentalDatabaseService database,
            IAuthService authService,
            IPhotoImageProcessor imageProcessor,
            ILogger<PhotoGalleryService> logger,
            string storageRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);

            _database = database;
            _authService = authService;
            _imageProcessor = imageProcessor;
            _logger = logger;
            _storageRoot = Path.GetFullPath(storageRoot);
        }

        // =====================================================================
        // Read
        // =====================================================================

        public async Task<RoomGallery?> GetGalleryAsync(int roomId)
        {
            var room = await LoadRoomMediaAsync(roomId);
            if (room is null)
            {
                return null;
            }

            var photos = room.PhotoKeys
                .Select((key, index) => ToRoomPhoto(key, isPanorama: false, index))
                .ToList();

            var panorama = string.IsNullOrWhiteSpace(room.PanoramaKey)
                ? null
                : ToRoomPhoto(room.PanoramaKey, isPanorama: true, sortOrder: -1);

            return new RoomGallery(roomId, photos, panorama);
        }

        // =====================================================================
        // Upload
        // =====================================================================

        public async Task<PhotoOperationResult> AddPhotoAsync(
            int roomId, Stream content, string fileName, bool isPanorama, CancellationToken cancellationToken = default)
        {
            if (roomId <= 0 || content is null || !content.CanRead)
            {
                return PhotoOperationResult.Failure(PhotoErrorCode.InvalidInput, "Dữ liệu ảnh gửi lên không hợp lệ.");
            }

            // 1. Read with a hard size cap (never trust Stream.Length alone).
            var (bytes, tooLarge) = await ReadWithLimitAsync(content, PhotoGalleryLimits.MaxFileSizeBytes, cancellationToken);
            if (tooLarge)
            {
                return PhotoOperationResult.Failure(PhotoErrorCode.FileTooLarge,
                    $"Ảnh vượt quá dung lượng tối đa {PhotoGalleryLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
            }

            if (bytes.Length == 0)
            {
                return PhotoOperationResult.Failure(PhotoErrorCode.InvalidInput, "File ảnh trống.");
            }

            // 2. Check the real content (magic bytes), not the file extension.
            if (!IsSupportedImageSignature(bytes))
            {
                _logger.LogInformation("Rejected upload {FileName}: not JPEG/PNG/WebP content.", Path.GetFileName(fileName));
                return PhotoOperationResult.Failure(PhotoErrorCode.UnsupportedType, "Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP.");
            }

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                // 3. Who may upload: landlord of this room or Admin.
                var (room, authError) = await AuthorizeAsync(roomId);
                if (authError is not null)
                {
                    return authError;
                }

                if (!isPanorama && room!.PhotoKeys.Count >= PhotoGalleryLimits.MaxPhotosPerRoom)
                {
                    return PhotoOperationResult.Failure(PhotoErrorCode.LimitReached,
                        $"Mỗi phòng chỉ được tối đa {PhotoGalleryLimits.MaxPhotosPerRoom} ảnh.");
                }

                // 4. Decode + re-encode (strips EXIF/GPS) + thumbnail on the platform codec.
                //    A renamed or corrupted file fails here.
                ProcessedPhoto? processed;
                try
                {
                    processed = await _imageProcessor.ProcessAsync(
                        bytes, PhotoGalleryLimits.ThumbnailMaxWidth, PhotoGalleryLimits.OriginalJpegQuality, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not decode uploaded image for room {RoomId}.", roomId);
                    processed = null;
                }

                if (processed is null || processed.Width < 1 || processed.Height < 1)
                {
                    return PhotoOperationResult.Failure(PhotoErrorCode.UnsupportedType, "File ảnh bị hỏng hoặc không đọc được.");
                }

                if (isPanorama && !IsPanoramaShape(processed.Width, processed.Height))
                {
                    return PhotoOperationResult.Failure(PhotoErrorCode.InvalidInput,
                        "Ảnh 360° phải có tỉ lệ 2:1 (ví dụ 4096×2048).");
                }

                // 5. Save original + thumbnail with a GUID name (never the user's file name).
                var key = $"{PhotoFolder}/{roomId}/{Guid.NewGuid():N}{StoredExtension}";
                var originalPath = ResolveLocalPath(key)!;
                var thumbnailPath = GetThumbnailPath(originalPath);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
                    await File.WriteAllBytesAsync(originalPath, processed.OriginalJpeg, cancellationToken);
                    await File.WriteAllBytesAsync(thumbnailPath, processed.ThumbnailJpeg, cancellationToken);

                    // 6. Persist the key.
                    string? replacedPanorama = null;
                    if (isPanorama)
                    {
                        replacedPanorama = room!.PanoramaKey;
                        await SaveRoomMediaAsync(roomId, room.PhotoKeys, key);
                    }
                    else
                    {
                        var keys = new List<string>(room!.PhotoKeys) { key };
                        await SaveRoomMediaAsync(roomId, keys, room.PanoramaKey);
                    }

                    // Old 360° file is deleted only after the database points to the new one.
                    DeleteLocalFiles(replacedPanorama);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
                {
                    _logger.LogError(ex, "Failed to store photo for room {RoomId}.", roomId);
                    DeleteLocalFiles(key); // roll back files that were written
                    return PhotoOperationResult.Failure(PhotoErrorCode.StorageFailed,
                        "Không lưu được ảnh. Vui lòng thử lại.");
                }

                var sortOrder = isPanorama ? -1 : room.PhotoKeys.Count;
                _logger.LogInformation("Stored {Kind} photo {Key} for room {RoomId}.",
                    isPanorama ? "360°" : "2D", key, roomId);

                return PhotoOperationResult.Success(
                    isPanorama ? "Đã cập nhật ảnh 360°." : "Đã tải ảnh lên.",
                    ToRoomPhoto(key, isPanorama, sortOrder));
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // =====================================================================
        // Remove / Reorder
        // =====================================================================

        public async Task<PhotoOperationResult> RemovePhotoAsync(int roomId, string photoKey)
        {
            if (roomId <= 0 || string.IsNullOrWhiteSpace(photoKey))
            {
                return PhotoOperationResult.Failure(PhotoErrorCode.InvalidInput, "Thiếu thông tin ảnh cần xóa.");
            }

            await _writeLock.WaitAsync();
            try
            {
                var (room, authError) = await AuthorizeAsync(roomId);
                if (authError is not null)
                {
                    return authError;
                }

                var keys = new List<string>(room!.PhotoKeys);
                var panorama = room.PanoramaKey;

                if (string.Equals(panorama, photoKey, StringComparison.Ordinal))
                {
                    panorama = string.Empty;
                }
                else if (!keys.Remove(photoKey)) // removes the first match (seed data may repeat a value)
                {
                    return PhotoOperationResult.Failure(PhotoErrorCode.RoomNotFound, "Không tìm thấy ảnh cần xóa.");
                }

                try
                {
                    await SaveRoomMediaAsync(roomId, keys, panorama);
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Failed to remove photo {Key} from room {RoomId}.", photoKey, roomId);
                    return PhotoOperationResult.Failure(PhotoErrorCode.StorageFailed, "Không xóa được ảnh. Vui lòng thử lại.");
                }

                // Delete files only if no other entry still points to the same key.
                if (!keys.Contains(photoKey) && panorama != photoKey)
                {
                    DeleteLocalFiles(photoKey);
                }

                _logger.LogInformation("Removed photo {Key} from room {RoomId}.", photoKey, roomId);
                return PhotoOperationResult.Success("Đã xóa ảnh.");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<PhotoOperationResult> ReorderPhotosAsync(int roomId, IReadOnlyList<string> orderedKeys)
        {
            if (roomId <= 0 || orderedKeys is null)
            {
                return PhotoOperationResult.Failure(PhotoErrorCode.InvalidInput, "Thứ tự ảnh không hợp lệ.");
            }

            await _writeLock.WaitAsync();
            try
            {
                var (room, authError) = await AuthorizeAsync(roomId);
                if (authError is not null)
                {
                    return authError;
                }

                // Must be the same multiset of keys: no additions, no removals.
                var current = room!.PhotoKeys.OrderBy(k => k, StringComparer.Ordinal);
                var requested = orderedKeys.OrderBy(k => k, StringComparer.Ordinal);
                if (!current.SequenceEqual(requested, StringComparer.Ordinal))
                {
                    return PhotoOperationResult.Failure(PhotoErrorCode.InvalidInput,
                        "Danh sách sắp xếp phải chứa đúng các ảnh hiện có của phòng.");
                }

                try
                {
                    await SaveRoomMediaAsync(roomId, orderedKeys, room.PanoramaKey);
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Failed to reorder photos of room {RoomId}.", roomId);
                    return PhotoOperationResult.Failure(PhotoErrorCode.StorageFailed, "Không lưu được thứ tự ảnh.");
                }

                return PhotoOperationResult.Success("Đã cập nhật thứ tự ảnh.");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // =====================================================================
        // Authorization
        // =====================================================================

        /// <summary>
        /// Checks in HTTP order: 401 (not signed in) → 404 (room) → 403 (not owner/admin).
        /// IAuthService is constructor-injected (DIP); with the InMemoryAuthService fallback it still works.
        /// </summary>
        private async Task<(RoomMedia? Room, PhotoOperationResult? Error)> AuthorizeAsync(int roomId)
        {
            var user = _authService.CurrentUser;
            if (user is null)
            {
                return (null, PhotoOperationResult.Failure(PhotoErrorCode.NotSignedIn,
                    "Bạn cần đăng nhập để quản lý ảnh phòng."));
            }

            var room = await LoadRoomMediaAsync(roomId);
            if (room is null)
            {
                return (null, PhotoOperationResult.Failure(PhotoErrorCode.RoomNotFound, "Không tìm thấy phòng."));
            }

            var isAdmin = string.Equals(user.Role, Constants.UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
            var isOwner = string.Equals(user.Role, Constants.UserRoles.Landlord, StringComparison.OrdinalIgnoreCase)
                          && user.Id == room.LandlordId;
            if (!isAdmin && !isOwner)
            {
                _logger.LogWarning("User {UserId} tried to change photos of room {RoomId} without permission.", user.Id, roomId);
                return (null, PhotoOperationResult.Failure(PhotoErrorCode.Forbidden,
                    "Chỉ chủ phòng hoặc quản trị viên được thay đổi ảnh."));
            }

            return (room, null);
        }

        // =====================================================================
        // Database (only PhotosJson, Panorama360Url, LandlordId of RoomListing)
        // =====================================================================

        private sealed record RoomMedia(IReadOnlyList<string> PhotoKeys, string PanoramaKey, int LandlordId);

        private async Task<RoomMedia?> LoadRoomMediaAsync(int roomId)
        {
            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT PhotosJson, Panorama360Url, LandlordId FROM RoomListing WHERE Id = @id LIMIT 1;";
            command.Parameters.AddWithValue("@id", roomId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var photosJson = reader.IsDBNull(0) ? "[]" : reader.GetString(0);
            var panorama = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            return new RoomMedia(ParsePhotoKeys(photosJson, roomId), panorama, reader.GetInt32(2));
        }

        private async Task SaveRoomMediaAsync(int roomId, IReadOnlyList<string> photoKeys, string panoramaKey)
        {
            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE RoomListing SET PhotosJson = @photos, Panorama360Url = @panorama WHERE Id = @id;";
            command.Parameters.AddWithValue("@photos", SerializePhotoKeys(photoKeys));
            command.Parameters.AddWithValue("@panorama", panoramaKey ?? string.Empty);
            command.Parameters.AddWithValue("@id", roomId);
            await command.ExecuteNonQueryAsync();
        }

        // JsonDocument / Utf8JsonWriter avoid reflection-based serialization (safe with trimming on Android).
        private List<string> ParsePhotoKeys(string json, int roomId)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("PhotosJson of room {RoomId} is not an array; treating as empty.", roomId);
                    return [];
                }

                return document.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "PhotosJson of room {RoomId} is invalid JSON; treating as empty.", roomId);
                return [];
            }
        }

        private static string SerializePhotoKeys(IReadOnlyList<string> keys)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartArray();
                foreach (var key in keys)
                {
                    writer.WriteStringValue(key);
                }
                writer.WriteEndArray();
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }

        // =====================================================================
        // Files & images
        // =====================================================================

        private RoomPhoto ToRoomPhoto(string key, bool isPanorama, int sortOrder)
        {
            var localPath = ResolveLocalPath(key);
            if (localPath is null)
            {
                // Seed/bundled image or remote URL: no generated thumbnail.
                return new RoomPhoto(key, key, key, isPanorama, sortOrder);
            }

            var thumbnailPath = GetThumbnailPath(localPath);
            var thumbnail = File.Exists(thumbnailPath) ? thumbnailPath : localPath;
            return new RoomPhoto(key, localPath, thumbnail, isPanorama, sortOrder);
        }

        /// <summary>
        /// Maps a key like "room-photos/3/abc.jpg" to a full path inside the storage root.
        /// Returns null for keys that are not ours, or that would escape the root (path traversal).
        /// </summary>
        private string? ResolveLocalPath(string key)
        {
            if (!key.StartsWith(PhotoFolder + "/", StringComparison.Ordinal))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, key));
            var allowedRoot = Path.Combine(_storageRoot, PhotoFolder) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        private static string GetThumbnailPath(string originalPath) =>
            Path.Combine(Path.GetDirectoryName(originalPath)!,
                Path.GetFileNameWithoutExtension(originalPath) + ThumbnailSuffix + StoredExtension);

        private void DeleteLocalFiles(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var originalPath = ResolveLocalPath(key);
            if (originalPath is null)
            {
                return; // seed/remote image: nothing on disk to delete
            }

            foreach (var path in new[] { originalPath, GetThumbnailPath(originalPath) })
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Orphan file only wastes space; the database is already consistent.
                    _logger.LogWarning(ex, "Could not delete photo file {Path}.", path);
                }
            }
        }

        private static bool IsPanoramaShape(float width, float height) =>
            Math.Abs(width / height - PhotoGalleryLimits.PanoramaAspectRatio)
            <= PhotoGalleryLimits.PanoramaAspectRatio * PhotoGalleryLimits.PanoramaAspectTolerance;

        /// <summary>Reads at most maxBytes; reports tooLarge if the stream has more.</summary>
        private static async Task<(byte[] Bytes, bool TooLarge)> ReadWithLimitAsync(
            Stream content, long maxBytes, CancellationToken cancellationToken)
        {
            if (content.CanSeek && content.Length - content.Position > maxBytes)
            {
                return ([], true);
            }

            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    return ([], true);
                }
                buffer.Write(chunk, 0, read);
            }

            return (buffer.ToArray(), false);
        }

        /// <summary>True if the file signature (magic bytes) is JPEG, PNG or WebP; the extension is never trusted.</summary>
        internal static bool IsSupportedImageSignature(ReadOnlySpan<byte> data)
        {
            // JPEG: FF D8 FF
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            {
                return true;
            }

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                return true;
            }

            // WebP: "RIFF" <4-byte size> "WEBP"
            return data.Length >= 12
                   && data[..4].SequenceEqual("RIFF"u8)
                   && data.Slice(8, 4).SequenceEqual("WEBP"u8);
        }
    }
}
