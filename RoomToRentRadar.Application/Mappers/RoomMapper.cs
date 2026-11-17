namespace RoomToRentRadar.Application.Mappers;

using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Pure mapping functions between Domain aggregates/candidates and Response DTOs.
/// Adheres strictly to BR-LST-03 for utility estimates and BR-SRCH-03 for distance precision.
/// </summary>
public static class RoomMapper
{
    public static RoomDetailDto ToDetailDto(
        Room room,
        User? landlord,
        Campus campus,
        bool isPhoneVisible)
    {
        throw new NotImplementedException();
    }

    public static RoomCardDto ToCardDto(
        RoomSearchCandidate candidate,
        Campus campus)
    {
        throw new NotImplementedException();
    }
}
