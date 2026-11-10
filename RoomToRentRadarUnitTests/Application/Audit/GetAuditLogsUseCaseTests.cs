using RoomToRentRadar.Application.UseCases.Audit;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Audit;

[TestFixture]
public class GetAuditLogsUseCaseTests
{
    private IAuditLogRepository _repository = null!;
    private GetAuditLogsUseCase _sut = null!;
    private static readonly DateTime FixedNow = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IAuditLogRepository>();
        _sut = new GetAuditLogsUseCase();
    }

    [Test]
    public async Task ExecuteAsync_WithFilterAndPagination_DelegatesToRepositoryAndMapsDtos()
    {
        var request = new AuditLogQueryRequest(
            UserId: "user-123",
            Action: "ROOM_CREATED",
            EntityType: "Room",
            FromUtc: FixedNow.AddHours(-1),
            ToUtc: FixedNow,
            PageNumber: 1,
            PageSize: 10);

        var domainEntry = new AuditLogEntry(
            id: "log-001",
            userId: "user-123",
            userEmail: "landlord@gmail.com",
            role: "Landlord",
            action: "ROOM_CREATED",
            entityType: "Room",
            entityId: "room-999",
            detailsJson: "{\"price\":2000000}",
            ipAddress: "127.0.0.1",
            userAgent: "Postman",
            statusCode: 201,
            durationMs: 40,
            isSuccess: true,
            errorMessage: null,
            timestampUtc: FixedNow);

        var pagedEntries = new PagedResult<AuditLogEntry>(new[] { domainEntry }, 1, 1, 10);
        _repository.QueryAsync(request, Arg.Any<CancellationToken>()).Returns(pagedEntries);

        var result = await _sut.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("log-001"));
            Assert.That(result.Items[0].UserId, Is.EqualTo("user-123"));
            Assert.That(result.Items[0].Action, Is.EqualTo("ROOM_CREATED"));
            Assert.That(result.Items[0].Role, Is.EqualTo("Landlord"));
            Assert.That(result.Items[0].IsSuccess, Is.True);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenNoLogsFound_ReturnsEmptyPagedResult()
    {
        var request = new AuditLogQueryRequest(UserId: "non-existent");
        var emptyPaged = new PagedResult<AuditLogEntry>(Array.Empty<AuditLogEntry>(), 0, 1, 50);

        _repository.QueryAsync(request, Arg.Any<CancellationToken>()).Returns(emptyPaged);

        var result = await _sut.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.Items, Is.Empty);
        });
    }
}

