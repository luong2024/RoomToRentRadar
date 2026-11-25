using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Retrieves cached or routed travel times from a room to the resolved campus (US09, UC-12, BR-TRV-01..04).
/// Enforces 30-day cache TTL, Bus estimation formula (<c>driving * 1.6 + 10 min</c>), profile campus
/// selection, and straight-line Haversine fallback when routing is unavailable.
/// </summary>
public sealed class GetRoomTravelTimesUseCase : IGetRoomTravelTimesUseCase
{
    private const int CacheTtlDays = 30;
    private const int BusOverheadSeconds = 600;
    private const double BusDurationFactor = 1.6;
    private const string SourceRouting = "Routing";
    private const string SourceEstimated = "Estimated";

    private static readonly TravelMode[] OrderedModes =
    [
        TravelMode.Motorbike,
        TravelMode.Bus,
        TravelMode.Walking
    ];

    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly ICurrentUserContextPort _currentUserContext;
    private readonly ITravelTimeCacheRepository _travelTimeCacheRepository;
    private readonly ITravelTimePort _travelTimePort;
    private readonly IClock _clock;

    public GetRoomTravelTimesUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        ICampusRepository campusRepository,
        ICurrentUserContextPort currentUserContext,
        ITravelTimeCacheRepository travelTimeCacheRepository,
        ITravelTimePort travelTimePort,
        IClock clock)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _travelTimeCacheRepository = travelTimeCacheRepository ?? throw new ArgumentNullException(nameof(travelTimeCacheRepository));
        _travelTimePort = travelTimePort ?? throw new ArgumentNullException(nameof(travelTimePort));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TravelTimesDto> ExecuteAsync(
        string roomId,
        string? campusId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new EntityNotFoundException(ProblemCodes.RoomNotFound, "Room ID is required.");
        }

        var room = await _roomRepository.GetByIdAsync(roomId.Trim(), ct).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(ProblemCodes.RoomNotFound, $"Room with ID '{roomId}' was not found.");

        RoomQueryHelper.EnsureViewable(room, roomId, _currentUserContext);

        var campus = await RoomQueryHelper.ResolveCampusAsync(
            campusId, _campusRepository, _userRepository, _currentUserContext, ct).ConfigureAwait(false);

        var straightLineKm = HaversineDistanceCalculator.CalculateDistanceKm(
            campus.Latitude, campus.Longitude, room.Latitude, room.Longitude);

        var now = _clock.UtcNow;
        var cachedEntries = await _travelTimeCacheRepository
            .GetByRoomAndCampusAsync(room.Id, campus.Id, ct)
            .ConfigureAwait(false);

        if (TryBuildValidCachedItems(cachedEntries, now, out var cachedItems))
        {
            return new TravelTimesDto(campus.Id, straightLineKm, IsFallback: false, cachedItems);
        }

        var routed = await _travelTimePort
            .GetRoutesAsync(room.Latitude, room.Longitude, campus.Latitude, campus.Longitude, ct)
            .ConfigureAwait(false);

        if (routed is null)
        {
            // BR-TRV-04 / ERD §4.15: straight-line fallback is never cached.
            return new TravelTimesDto(campus.Id, straightLineKm, IsFallback: true, Array.Empty<TravelTimeItemDto>());
        }

        var busDurationSeconds = (int)Math.Round(
            routed.Driving.DurationSeconds * BusDurationFactor, MidpointRounding.AwayFromZero) + BusOverheadSeconds;

        var newEntries = new[]
        {
            new TravelTimeCacheEntry(
                room.Id, campus.Id, TravelMode.Motorbike,
                routed.Driving.DistanceMeters, routed.Driving.DurationSeconds, SourceRouting, now),
            new TravelTimeCacheEntry(
                room.Id, campus.Id, TravelMode.Bus,
                routed.Driving.DistanceMeters, busDurationSeconds, SourceEstimated, now),
            new TravelTimeCacheEntry(
                room.Id, campus.Id, TravelMode.Walking,
                routed.Walking.DistanceMeters, routed.Walking.DurationSeconds, SourceRouting, now)
        };

        await _travelTimeCacheRepository.UpsertRangeAsync(newEntries, ct).ConfigureAwait(false);

        var items = newEntries.Select(ToDto).ToList();
        return new TravelTimesDto(campus.Id, straightLineKm, IsFallback: false, items);
    }

    private static bool TryBuildValidCachedItems(
        IReadOnlyList<TravelTimeCacheEntry> entries,
        DateTime utcNow,
        out IReadOnlyList<TravelTimeItemDto> items)
    {
        items = Array.Empty<TravelTimeItemDto>();
        if (entries.Count < OrderedModes.Length)
        {
            return false;
        }

        var validCutoff = utcNow.AddDays(-CacheTtlDays);
        var byMode = new Dictionary<TravelMode, TravelTimeCacheEntry>(OrderedModes.Length);
        foreach (var entry in entries)
        {
            if (entry.ComputedAtUtc < validCutoff)
            {
                return false;
            }

            byMode[entry.Mode] = entry;
        }

        var ordered = new List<TravelTimeItemDto>(OrderedModes.Length);
        foreach (var mode in OrderedModes)
        {
            if (!byMode.TryGetValue(mode, out var entry))
            {
                return false;
            }

            ordered.Add(ToDto(entry));
        }

        items = ordered;
        return true;
    }

    private static TravelTimeItemDto ToDto(TravelTimeCacheEntry entry) => new(
        Mode: entry.Mode,
        DistanceMeters: entry.DistanceMeters,
        DurationSeconds: entry.DurationSeconds,
        IsEstimate: entry.Mode == TravelMode.Bus || string.Equals(entry.Source, SourceEstimated, StringComparison.Ordinal));
}
