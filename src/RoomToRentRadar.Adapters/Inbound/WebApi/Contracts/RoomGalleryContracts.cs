using Microsoft.AspNetCore.Http;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed class UploadRoomPhotoFormRequest
{
    public IFormFile? File { get; set; }

    public bool Is360 { get; set; }

    public bool IsCover { get; set; }

    public string? Caption { get; set; }
}

public sealed record ReorderRoomPhotosRequest(
    IReadOnlyList<string>? OrderedPhotoIds,
    string? CoverPhotoId = null);

public sealed record AssignRoomOwnerRequest(Guid LandlordId);
