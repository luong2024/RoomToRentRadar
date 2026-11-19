using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Ports.Inbound.UseCases.Campuses;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Campuses;

/// <summary>
/// Lists campuses ordered with the default campus (ICTU) first, then by SortOrder (BR-CMP-01, <c>docs/api/openapi.yaml:219</c>, <c>docs/erd.md</c> §4.1).
/// </summary>
public sealed class ListCampusesUseCase : IListCampusesUseCase
{
    private readonly ICampusRepository _campusRepository;

    public ListCampusesUseCase(ICampusRepository campusRepository)
    {
        ArgumentNullException.ThrowIfNull(campusRepository);
        _campusRepository = campusRepository;
    }

    public async Task<IReadOnlyList<CampusDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var campuses = await _campusRepository.GetAllAsync(ct);
        if (campuses.Count == 0)
        {
            return Array.Empty<CampusDto>();
        }

        return campuses
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(MapToDto)
            .ToList();
    }

    private static CampusDto MapToDto(Campus campus) => new(
        Id: campus.Id,
        Name: campus.Name,
        ShortName: campus.ShortName,
        Latitude: campus.Latitude,
        Longitude: campus.Longitude,
        IsDefault: campus.IsDefault);
}
