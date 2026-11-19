namespace RoomToRentRadar.Shared.Travel;

public sealed record TravelEstimateDto(
    TravelMode Mode,
    TravelSource Source,
    int DistanceM,
    int? DurationS);
