namespace RoomToRentRadar.Ports.Contracts.Responses.Favourites;

using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>FavouriteDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record FavouriteDto(
    RoomCardDto Room,
    DateTime SavedAtUtc
);
