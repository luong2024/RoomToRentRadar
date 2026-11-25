using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Reports;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.Tests;

[TestFixture]
public class ListingReportUseCasesTests
{
    private FakeRoomRepository _roomRepo = null!;
    private FakeListingReportRepository _reportRepo = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepo = new FakeRoomRepository();
        _reportRepo = new FakeListingReportRepository();
    }

    [Test]
    public async Task SubmitReport_WhenValid_CreatesPendingReportWithRoomSnapshot()
    {
        var landlordId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _roomRepo.Upsert(new Room
        {
            Id = "room-1",
            Title = "Phòng trọ khép kín",
            LandlordId = landlordId
        });

        var useCase = CreateSubmitUseCase();
        var result = await useCase.ExecuteAsync(new SubmitListingReportCommand(
            "room-1",
            studentId,
            ReportReason.InaccurateInfo,
            "Sai diện tích"));

        var saved = await _reportRepo.GetByIdAsync(result.ReportId, "room-1");
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ReportStatus.Pending));
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.RoomTitleSnapshot, Is.EqualTo("Phòng trọ khép kín"));
            Assert.That(saved.RoomLandlordId, Is.EqualTo(landlordId));
        });
    }

    [Test]
    public void SubmitReport_WhenRoomMissing_ThrowsNotFoundException()
    {
        var useCase = CreateSubmitUseCase();
        Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(
            new SubmitListingReportCommand("missing-room", Guid.NewGuid(), ReportReason.SuspectedScam, null)));
    }

    [Test]
    public void SubmitReport_WhenReporterIsRoomLandlord_ThrowsReportDomainException()
    {
        var landlordId = Guid.NewGuid();
        _roomRepo.Upsert(new Room { Id = "room-1", Title = "Phòng 1", LandlordId = landlordId });

        var useCase = CreateSubmitUseCase();
        Assert.ThrowsAsync<ReportDomainException>(() => useCase.ExecuteAsync(
            new SubmitListingReportCommand("room-1", landlordId, ReportReason.Other, "Test")));
    }

    [Test]
    public async Task SubmitReport_WhenActiveReportExists_ThrowsDuplicateReportException()
    {
        var studentId = Guid.NewGuid();
        _roomRepo.Upsert(new Room { Id = "room-1", Title = "Phòng 1", LandlordId = Guid.NewGuid() });
        var useCase = CreateSubmitUseCase();

        await useCase.ExecuteAsync(new SubmitListingReportCommand("room-1", studentId, ReportReason.AlreadyRented, null));

        Assert.ThrowsAsync<DuplicateReportException>(() => useCase.ExecuteAsync(
            new SubmitListingReportCommand("room-1", studentId, ReportReason.InaccurateInfo, null)));
    }

    [Test]
    public void SubmitReport_WhenCustomOcpRuleAdded_EvaluatesNewRuleWithoutModifyingUseCase()
    {
        _roomRepo.Upsert(new Room { Id = "room-1", Title = "Phòng 1", LandlordId = Guid.NewGuid() });
        var customRule = new RejectBlockedReporterRule();
        var useCase = new SubmitListingReportUseCase(
            _roomRepo,
            _reportRepo,
            [new PreventSelfReportRule(), new PreventDuplicateActiveReportRule(_reportRepo), customRule],
            TimeProvider.System);

        Assert.ThrowsAsync<ReportDomainException>(() => useCase.ExecuteAsync(
            new SubmitListingReportCommand("room-1", Guid.NewGuid(), ReportReason.SuspectedScam, null)));
    }

    [Test]
    public async Task ReviewReport_WhenETagMismatches_ThrowsConcurrencyConflictException()
    {
        var report = ListingReport.Rehydrate(
            "report:1",
            "room-1",
            "Phòng 1",
            null,
            Guid.NewGuid(),
            ReportReason.SuspectedScam,
            null,
            ReportStatus.Pending,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            etag: "etag-1");
        await _reportRepo.AddAsync(report);

        var reviewUseCase = new ReviewListingReportUseCase(_reportRepo, TimeProvider.System);
        Assert.ThrowsAsync<ConcurrencyConflictException>(() => reviewUseCase.ExecuteAsync(
            new ReviewListingReportCommand("report:1", "room-1", Guid.NewGuid(), ReportStatus.Dismissed, "OK", "stale-etag")));
    }

    private SubmitListingReportUseCase CreateSubmitUseCase() =>
        new(
            _roomRepo,
            _reportRepo,
            [new PreventSelfReportRule(), new PreventDuplicateActiveReportRule(_reportRepo)],
            TimeProvider.System);

    private sealed class RejectBlockedReporterRule : IReportSubmissionRule
    {
        public Task ValidateAsync(Room room, SubmitListingReportCommand command, CancellationToken cancellationToken = default) =>
            throw new ReportDomainException("Reporter is blocked.");
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

        public void Upsert(Room room) => _rooms[room.Id] = room;

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rooms.TryGetValue(id, out var room) ? room : null);

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>([]);

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Room>, int)>(([], 0));
    }

    private sealed class FakeListingReportRepository : IListingReportRepository
    {
        private readonly List<ListingReport> _items = [];

        public Task<ListingReport?> GetByIdAsync(string reportId, string roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(r => r.Id == reportId && r.RoomId == roomId));

        public Task<bool> HasActiveReportByUserAsync(string roomId, Guid reporterUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Any(r =>
                r.RoomId == roomId &&
                r.ReporterUserId == reporterUserId &&
                r.Status is ReportStatus.Pending or ReportStatus.InReview));

        public Task AddAsync(ListingReport report, CancellationToken cancellationToken = default)
        {
            _items.Add(report);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ListingReport report, CancellationToken cancellationToken = default)
        {
            var idx = _items.FindIndex(r => r.Id == report.Id && r.RoomId == report.RoomId);
            if (idx >= 0)
            {
                _items[idx] = report;
            }

            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<ListingReport> Items, int TotalCount)> GetModerationQueueAsync(
            ReportStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = _items
                .Where(r => !status.HasValue || r.Status == status.Value)
                .OrderBy(r => r.CreatedAtUtc)
                .ToList();

            var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<ListingReport>, int)>((paged, filtered.Count));
        }
    }
}
