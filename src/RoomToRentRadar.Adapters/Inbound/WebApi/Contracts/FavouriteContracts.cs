namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed record AddFavouriteRequest(string? RoomId);

public sealed record SyncFavouritesRequest(IReadOnlyCollection<string?>? RoomIds);
