using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.HouseRules;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Tests;

public class HouseRulesUseCasesTests
{
    private FakeRoomRepository _roomRepo = null!;
    private FakeHouseRulesRepository _houseRulesRepo = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepo = new FakeRoomRepository();
        _houseRulesRepo = new FakeHouseRulesRepository();
    }

    [Test]
    public async Task GetRoomHouseRules_WhenNoStructuredRulesExist_ShouldReturnLegacyFallback()
    {
        var room = new Room
        {
            Id = "room-legacy",
            HouseRules = ["No smoking", "Curfew 23:00"]
        };
        _roomRepo.Seed(room);

        var useCase = new GetRoomHouseRulesUseCase(_roomRepo, _houseRulesRepo);

        var result = await useCase.ExecuteAsync("room-legacy");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsLegacyFallback, Is.True);
            Assert.That(result.Version, Is.EqualTo(0));
            Assert.That(result.AdditionalRules, Is.EqualTo(new[] { "No smoking", "Curfew 23:00" }));
        });
    }

    [Test]
    public async Task UpdateRoomHouseRules_WhenExpectedVersionMismatches_ShouldThrowConcurrencyConflict()
    {
        var landlordId = Guid.NewGuid();
        var room = new Room { Id = "room-1", LandlordId = landlordId };
        _roomRepo.Seed(room);

        var policy = HouseRulesPolicy.Create("22:00", "No guests", "Electric only", "1 bike");
        var (existing, rev1) = RoomHouseRules.CreateInitial("room-1", landlordId, policy, landlordId, false, null, DateTimeOffset.UtcNow);
        await _houseRulesRepo.SaveAsync(existing, rev1);

        var useCase = new UpdateRoomHouseRulesUseCase(_roomRepo, _houseRulesRepo, TimeProvider.System);

        var cmd = new UpdateHouseRulesCommand(
            "room-1",
            landlordId,
            UserRoleExtensions.LandlordRoleName,
            ExpectedVersion: 99,
            "23:00",
            "Guests until 21:00",
            "Allowed",
            "1 bike",
            null,
            "Update");

        Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(cmd));
    }

    [Test]
    public void UpdateRoomHouseRules_WhenDifferentLandlord_ShouldThrowOwnershipException()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        _roomRepo.Seed(new Room { Id = "room-1", LandlordId = ownerId });

        var useCase = new UpdateRoomHouseRulesUseCase(_roomRepo, _houseRulesRepo, TimeProvider.System);

        var cmd = new UpdateHouseRulesCommand(
            "room-1",
            otherId,
            UserRoleExtensions.LandlordRoleName,
            ExpectedVersion: null,
            "23:00",
            "Guests until 21:00",
            "Allowed",
            "1 bike",
            null,
            null);

        Assert.ThrowsAsync<HouseRulesOwnershipException>(() => useCase.ExecuteAsync(cmd));
    }

    [Test]
    public async Task UpdateRoomHouseRules_WhenPolicyUnchanged_ShouldNotIncrementVersionOrSave()
    {
        var landlordId = Guid.NewGuid();
        var room = new Room { Id = "room-1", LandlordId = landlordId };
        _roomRepo.Seed(room);

        var policy = HouseRulesPolicy.Create("22:00", "No guests", "Electric only", "1 bike", ["No pets"]);
        var (existing, rev1) = RoomHouseRules.CreateInitial("room-1", landlordId, policy, landlordId, false, null, DateTimeOffset.UtcNow);
        await _houseRulesRepo.SaveAsync(existing, rev1);

        var useCase = new UpdateRoomHouseRulesUseCase(_roomRepo, _houseRulesRepo, TimeProvider.System);

        var cmd = new UpdateHouseRulesCommand(
            "room-1",
            landlordId,
            UserRoleExtensions.LandlordRoleName,
            ExpectedVersion: 1,
            " 22:00 ",
            "No guests",
            "Electric only",
            "1 bike",
            ["No pets"],
            "No actual change");

        var result = await useCase.ExecuteAsync(cmd);
        var history = await _houseRulesRepo.GetRevisionsByRoomIdAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Version, Is.EqualTo(1));
            Assert.That(result.LandlordId, Is.EqualTo(landlordId));
            Assert.That(history, Has.Count.EqualTo(1));
        });
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(Room room) => _rooms[room.Id] = room;

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rooms.TryGetValue(id, out var room) ? room : null);

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>([]);

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Room>, int)>(([], 0));
    }

    private sealed class FakeHouseRulesRepository : IHouseRulesRepository
    {
        private readonly Dictionary<string, RoomHouseRules> _current = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<HouseRulesRevision>> _revisions = new(StringComparer.OrdinalIgnoreCase);

        public Task<RoomHouseRules?> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_current.TryGetValue(roomId, out var rules) ? rules : null);

        public Task<IReadOnlyList<HouseRulesRevision>> GetRevisionsByRoomIdAsync(string roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HouseRulesRevision>>(
                _revisions.TryGetValue(roomId, out var list) ? list.OrderByDescending(r => r.Version).ToList() : []);

        public Task SaveAsync(RoomHouseRules houseRules, HouseRulesRevision newRevision, CancellationToken cancellationToken = default)
        {
            _current[houseRules.RoomId] = houseRules;
            if (!_revisions.TryGetValue(houseRules.RoomId, out var list))
            {
                list = [];
                _revisions[houseRules.RoomId] = list;
            }
            list.Add(newRevision);
            return Task.CompletedTask;
        }
    }
}
