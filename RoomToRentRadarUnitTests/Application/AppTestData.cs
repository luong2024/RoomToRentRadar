using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadarUnitTests.Application;

/// <summary>Runs the unit-of-work action inline so use-case tests observe the repository calls.</summary>
internal sealed class InlineUnitOfWork : IUnitOfWork
{
    public int Executions { get; private set; }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        Executions++;
        return action(ct);
    }
}

/// <summary>In-memory <see cref="IPhotoStoragePort"/>: records saved/deleted keys and maps URLs.</summary>
internal sealed class FakePhotoStorage : IPhotoStoragePort
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public List<string> DeletedKeys { get; } = new();

    public Exception? DeleteFailure { get; set; }

    public Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        Files[storageKey] = buffer.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        DeletedKeys.Add(storageKey);
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }

        Files.Remove(storageKey);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string storageKey) => "https://media.test/" + storageKey;
}

/// <summary>Builders shared by the US08 application tests (real magic-byte images, ERD §4.8 photos).</summary>
internal static class AppTestData
{
    public static readonly DateTime Now = DateTime.UtcNow;

    public static byte[] Jpeg(int width = 1280, int height = 720)
    {
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8]);
        // APP0: skipped by ImageHeaderReader and copied verbatim by the EXIF stripper.
        stream.Write([0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);
        // SOF0 carries precision + height + width.
        stream.Write([0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03]);
        stream.Write(new byte[9]);
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    public static byte[] Png(int width = 800, int height = 600)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        stream.Write([0x00, 0x00, 0x00, 0x0D]);
        stream.Write("IHDR"u8);
        stream.Write([(byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width]);
        stream.Write([(byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height]);
        stream.Write([0x08, 0x02, 0x00, 0x00, 0x00]);
        stream.Write(new byte[4]); // CRC (not validated by the header reader)
        stream.Write([0x00, 0x00, 0x00, 0x00]);
        stream.Write("IEND"u8);
        stream.Write(new byte[4]);
        return stream.ToArray();
    }

    public static RoomPhoto Photo(
        string id,
        string ownerId = "landlord-1",
        string? roomId = null,
        PhotoKind kind = PhotoKind.Standard,
        DateTime? createdAtUtc = null,
        int sortOrder = 0) =>
        RoomPhoto.Reconstitute(
            id: id,
            roomId: roomId,
            uploadedByUserId: ownerId,
            kind: kind,
            contentType: RoomPhoto.JpegContentType,
            sizeBytes: 204_800,
            width: 1280,
            height: 720,
            originalPath: $"rooms/2026/11/{id}/original.jpg",
            mediumPath: $"rooms/2026/11/{id}/medium.jpg",
            thumbPath: $"rooms/2026/11/{id}/thumb.jpg",
            sortOrder: sortOrder,
            createdAtUtc: createdAtUtc ?? Now);

    public static Room NewRoom(
        string id = "room-1",
        string landlordId = "landlord-1",
        ListingStatus status = ListingStatus.PendingReview,
        int version = 1,
        DateTime? lastUpdatedAtUtc = null,
        IReadOnlyList<Amenity>? amenities = null,
        IReadOnlyList<RoomSafetyFeatureEntry>? safetyFeatures = null,
        HouseRuleVersion? houseRules = null,
        string? statusReason = null) =>
        Room.Reconstitute(
            id: id,
            landlordId: landlordId,
            title: "Phòng kiểm thử US08",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 20.0,
            addressLine: "Tân Thịnh",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            status: status,
            createdAtUtc: Now.AddDays(-1),
            lastUpdatedAtUtc: lastUpdatedAtUtc ?? Now,
            version: version,
            statusReason: statusReason,
            amenities: amenities,
            safetyFeatures: safetyFeatures,
            currentHouseRules: houseRules);

    public static HouseRulesFieldsDto HouseRules() =>
        new(
            QuietHoursStart: "22:00",
            QuietHoursEnd: "06:00",
            CurfewTime: "23:30",
            GuestPolicy: GuestPolicy.DaytimeOnly,
            GuestPolicyNote: "Khách báo trước.",
            CookingPolicy: CookingPolicy.SharedKitchenOnly,
            ParkingAvailable: true,
            ParkingRules: "Xe máy xếp hàng.",
            PetsAllowed: false,
            SmokingAllowed: false,
            OtherRules: "Không tổ chức tiệc.");

    public static HouseRuleVersion HouseRulesVersion(string roomId = "room-1", int versionNumber = 1) =>
        HouseRuleVersion.Create(
            roomId: roomId,
            versionNumber: versionNumber,
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.SharedKitchenOnly,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-1",
            utcNow: Now.AddDays(-1),
            quietHoursStart: "22:00",
            quietHoursEnd: "06:00",
            curfewTime: "23:30",
            guestPolicyNote: "Khách báo trước.",
            parkingRules: "Xe máy xếp hàng.",
            otherRules: "Không tổ chức tiệc.");

    /// <summary>Same photo, now attached to a room with the given sort order (the attach outcome).</summary>
    public static RoomPhoto Attached(RoomPhoto photo, string roomId, int sortOrder = 0) =>
        RoomPhoto.Reconstitute(
            id: photo.Id,
            roomId: roomId,
            uploadedByUserId: photo.UploadedByUserId,
            kind: photo.Kind,
            contentType: photo.ContentType,
            sizeBytes: photo.SizeBytes,
            width: photo.Width,
            height: photo.Height,
            originalPath: photo.OriginalPath,
            mediumPath: photo.MediumPath,
            thumbPath: photo.ThumbPath,
            sortOrder: sortOrder,
            createdAtUtc: photo.CreatedAtUtc,
            attachedAtUtc: Now);

    public static UpsertListingRequest ListingRequest(
        IReadOnlyList<string> photoIds,
        IReadOnlyList<Amenity>? amenities = null,
        IReadOnlyList<SafetyFeature>? safetyFeatures = null,
        HouseRulesFieldsDto? houseRules = null,
        string addressLine = "Tân Thịnh",
        double latitude = 21.5851,
        double longitude = 105.8066,
        string title = "Phòng kiểm thử US08") =>
        new(
            Title: title,
            Description: "Phòng sạch sẽ gần trường.",
            RoomType: RoomType.BoardingRoom,
            MonthlyRentVnd: 1_800_000L,
            DepositVnd: 1_800_000L,
            AreaM2: 20.0,
            MaxOccupants: 2,
            Furnishing: Furnishing.Basic,
            AvailableFrom: new DateOnly(2026, 12, 1),
            Address: new AddressDto(addressLine, "Tân Thịnh", "TP. Thái Nguyên"),
            Location: new GeoPointDto(latitude, longitude),
            Utilities: new UtilitiesDto(3_800L, 70_000L, WaterPriceUnit.PerPerson, 120_000L, 60_000L, "Phí rác 25.000 đ"),
            Amenities: amenities,
            SafetyFeatures: safetyFeatures,
            PhotoIds: photoIds,
            HouseRules: houseRules);
}
