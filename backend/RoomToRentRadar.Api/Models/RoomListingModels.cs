namespace RoomToRentRadar.Api.Models
{
    // ============================================================================
    // US02 / T02.1 — "M" of the MVC Web API: response models for RoomListingController.
    // Errors reuse ErrorResponse from AuthModels.cs so every endpoint returns the same error shape.
    // ============================================================================

    /// <summary>
    /// One room card on the listing screen (US02 AC2: title, monthly rent, area, distance to campus,
    /// verification badge) plus a few display fields. Landlord name/phone are deliberately left out:
    /// the listing is public, and contact details belong to the room detail screen (US03 AC4 masking).
    /// </summary>
    public sealed record RoomCardResponse(
        int Id,
        string Title,
        decimal MonthlyRent,
        double Area,
        double DistanceToCampusKm,
        bool IsVerified,
        string ThumbnailUrl,
        string RoomType,
        string Address,
        string Ward,
        int WalkMinutes,
        int BusMinutes,
        int MotorbikeMinutes);

    /// <summary>
    /// One page of Available + Verified rooms (US02 AC4) with the paging values actually used.
    /// The repository contract has no total count, so HasMore = "this page is full, ask for the next one".
    /// </summary>
    public sealed record RoomListingPage(
        int PageIndex,
        int PageSize,
        string SortBy,
        bool HasMore,
        IReadOnlyList<RoomCardResponse> Items);
}
