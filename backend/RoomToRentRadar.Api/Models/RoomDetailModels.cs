namespace RoomToRentRadar.Api.Models
{
    // ============================================================================
    // US03 / T03.1 — "M" of the MVC Web API: response models for RoomDetailController.
    // Errors reuse ErrorResponse (AuthModels.cs); similar rooms reuse RoomCardResponse (RoomListingModels.cs, T02.1).
    // ============================================================================

    /// <summary>
    /// Full room detail (US03).
    /// AC1: description, rent, deposit, area, room type, amenities, address, landlord contact, house rules.
    /// AC2: <see cref="Status"/> and <see cref="UpdatedAt"/>.
    /// AC3: when <see cref="IsAvailable"/> is false, <see cref="StatusMessage"/> explains it and
    ///      <see cref="SimilarRooms"/> suggests other rooms (empty when the room is available).
    /// AC4: <see cref="Landlord"/> phone is masked for guests (no valid JWT).
    /// </summary>
    public sealed record RoomDetailResponse(
        int Id,
        string Title,
        string Description,
        decimal MonthlyRent,
        decimal Deposit,
        double Area,
        string RoomType,
        IReadOnlyList<string> Amenities,
        string Address,
        string Ward,
        double Latitude,
        double Longitude,
        double DistanceToCampusKm,
        int WalkMinutes,
        int BusMinutes,
        int MotorbikeMinutes,
        string ThumbnailUrl,
        bool IsVerified,
        string Status,
        bool IsAvailable,
        string? StatusMessage,
        string UpdatedAt,
        LandlordContactResponse Landlord,
        HouseRulesResponse HouseRules,
        IReadOnlyList<RoomCardResponse> SimilarRooms);

    /// <summary>AC4: <see cref="Phone"/> is "0912 *** 678" for guests, the full number for signed-in users.</summary>
    public sealed record LandlordContactResponse(
        string Name,
        string Phone,
        bool IsPhoneMasked);

    /// <summary>AC1: current house rules of the room (version history belongs to US12).</summary>
    public sealed record HouseRulesResponse(
        string QuietHours,
        string GuestPolicy,
        string CookingRules,
        string ParkingRules,
        int Version);
}
