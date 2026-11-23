using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.HouseRules;

public sealed class UpdateRoomHouseRulesUseCase : IUpdateRoomHouseRulesUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IHouseRulesRepository _houseRulesRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateRoomHouseRulesUseCase(
        IRoomRepository roomRepository,
        IHouseRulesRepository houseRulesRepository,
        TimeProvider timeProvider)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _houseRulesRepository = houseRulesRepository ?? throw new ArgumentNullException(nameof(houseRulesRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RoomHouseRulesDto> ExecuteAsync(UpdateHouseRulesCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new RequestValidationException("roomId", "Room ID is required.");
        }

        var trimmedRoomId = command.RoomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedRoomId, cancellationToken)
            ?? throw new NotFoundException($"Room '{trimmedRoomId}' was not found.");

        var policy = HouseRulesPolicy.Create(
            command.QuietHours,
            command.GuestPolicy,
            command.CookingRules,
            command.ParkingRules,
            command.AdditionalRules);

        var isAdmin = string.Equals(command.ActorRole, UserRoleExtensions.AdminRoleName, StringComparison.OrdinalIgnoreCase);
        var utcNow = _timeProvider.GetUtcNow();

        var existing = await _houseRulesRepository.GetByRoomIdAsync(room.Id, cancellationToken);

        RoomHouseRules updatedRules;
        HouseRulesRevision newRevision;

        if (existing is null)
        {
            if (command.ExpectedVersion is not null and not 0)
            {
                throw new ConcurrencyConflictException(
                    $"House rules version conflict for room '{room.Id}'. Expected version {command.ExpectedVersion.Value}, but no versioned rules exist yet.");
            }

            (updatedRules, newRevision) = RoomHouseRules.CreateInitial(
                room.Id,
                room.LandlordId,
                policy,
                command.ActorUserId,
                isAdmin,
                command.ChangeNote,
                utcNow);
        }
        else
        {
            existing.EnsureCanModify(command.ActorUserId, isAdmin);

            if (command.ExpectedVersion.HasValue && command.ExpectedVersion.Value != existing.CurrentVersion)
            {
                throw new ConcurrencyConflictException(
                    $"House rules version conflict for room '{room.Id}'. Expected version {command.ExpectedVersion.Value}, current version is {existing.CurrentVersion}.");
            }

            if (existing.CurrentPolicy.HasSameRules(policy))
            {
                return existing.ToDto();
            }

            newRevision = existing.UpdatePolicy(
                policy,
                command.ActorUserId,
                isAdmin,
                command.ChangeNote,
                utcNow);
            updatedRules = existing;
        }

        await _houseRulesRepository.SaveAsync(updatedRules, newRevision, cancellationToken);
        return updatedRules.ToDto();
    }
}
