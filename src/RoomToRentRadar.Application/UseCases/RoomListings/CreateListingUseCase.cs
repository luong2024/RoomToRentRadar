namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

public sealed class CreateListingUseCase : ICreateListingUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IListingImageStorageService _imageStorage;

    public CreateListingUseCase(IRoomRepository roomRepository, IListingImageStorageService imageStorage)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _imageStorage = imageStorage ?? throw new ArgumentNullException(nameof(imageStorage));
    }

    public async Task<ListingWriteResult> ExecuteAsync(Guid ownerId, ListingWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty) throw new RequestValidationException("ownerId", "A valid landlord identity is required.");
        ListingRequestValidator.Validate(request, requireNewPhotos: true);

        var id = Guid.NewGuid().ToString("N");
        var storedUrls = new List<string>();
        try
        {
            foreach (var photo in request.Photos)
            {
                await using var content = photo.OpenReadStream();
                storedUrls.Add(await _imageStorage.StoreAsync(ownerId, id, content, photo.FileName, photo.ContentType, photo.Length, cancellationToken));
            }

            var room = ListingRequestValidator.CreateRoom(id, ownerId, request, storedUrls);
            await _roomRepository.CreateAsync(room, cancellationToken);
            return new ListingWriteResult(id, room.Status.ToString(), room.ImageUrls);
        }
        catch
        {
            await ListingImageCleanup.DeleteBestEffortAsync(_imageStorage, storedUrls, cancellationToken);
            throw;
        }
    }
}

internal static class ListingRequestValidator
{
    private static readonly HashSet<string> RoomTypes = new(StringComparer.Ordinal)
    {
        "BoardingRoom", "MiniApartment", "WholeHouse", "Dormitory"
    };

    public static void Validate(ListingWriteRequest request, bool requireNewPhotos)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>();
        AddTextError(errors, "title", request.Title);
        AddTextError(errors, "address", request.Address);
        if (request.MonthlyRentVnd <= 0) errors["monthlyRentVnd"] = ["Rent must be greater than zero."];
        if (request.DepositVnd < 0) errors["depositVnd"] = ["Deposit cannot be negative."];
        if (!double.IsFinite(request.AreaM2) || request.AreaM2 <= 0) errors["areaM2"] = ["Area must be a finite number greater than zero."];
        if (string.IsNullOrWhiteSpace(request.RoomType) || !RoomTypes.Contains(request.RoomType)) errors["roomType"] = ["Room type is invalid."];
        if (request.Photos is null) errors["photos"] = ["At least three valid photos are required."];
        if (requireNewPhotos && request.Photos is { Count: < 3 }) errors["photos"] = ["At least three valid photos are required."];
        if (request.Photos is { Count: > ListingPhotoLimits.MaxCount })
            errors["photos"] = [$"No more than {ListingPhotoLimits.MaxCount} photos are allowed."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }

    public static Room CreateRoom(string id, Guid ownerId, ListingWriteRequest request, IReadOnlyList<string> imageUrls)
    {
        var now = DateTime.UtcNow;
        return new Room
        {
            Id = id,
            LandlordId = ownerId,
            Title = request.Title!.Trim(),
            Address = request.Address!.Trim(),
            Price = request.MonthlyRentVnd,
            Deposit = request.DepositVnd,
            Area = request.AreaM2,
            RoomType = request.RoomType!,
            Description = request.Description?.Trim() ?? string.Empty,
            ImageUrls = imageUrls.ToList(),
            ThumbnailUrl = imageUrls.FirstOrDefault(),
            IsVerified = false,
            Status = RoomStatus.Pending,
            CreatedAt = now,
            LastUpdatedDate = now
        };
    }

    private static void AddTextError(IDictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[field] = [$"{field} is required."];
    }
}

internal static class ListingImageCleanup
{
    public static async Task DeleteBestEffortAsync(IListingImageStorageService storage, IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        foreach (var url in urls)
        {
            try { await storage.DeleteAsync(url, CancellationToken.None); }
            catch { /* Best-effort compensation; storage reconciliation can remove an orphan later. */ }
        }
    }
}
