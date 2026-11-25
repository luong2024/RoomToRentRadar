using System.Net;
using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class CosmosListingReportRepositoryTests
{
    [Test]
    public async Task HasActiveReportByUserAsync_UsesQueryDefinition_AndNeverUsesInMemoryPredicate()
    {
        var containerMock = new Mock<ICosmosContainer<ListingReportDocument>>(MockBehavior.Strict);
        var dbContextMock = new Mock<ICosmosDbContext>();
        dbContextMock.SetupGet(x => x.ListingReports).Returns(containerMock.Object);

        QueryDefinition? capturedQuery = null;
        containerMock
            .Setup(c => c.QueryAsync(It.IsAny<QueryDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<QueryDefinition, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync([new ListingReportDocument { Id = "report:1", RoomId = "room-1" }]);

        var repo = new CosmosListingReportRepository(dbContextMock.Object);
        var exists = await repo.HasActiveReportByUserAsync("room-1", Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(exists, Is.True);
            Assert.That(capturedQuery, Is.Not.Null);
            Assert.That(capturedQuery!.QueryText, Does.Contain("c.roomId = @roomId"));
            Assert.That(capturedQuery.QueryText, Does.Contain("c.reporterUserId = @reporterUserId"));
        });

        containerMock.Verify(
            c => c.QueryAsync(It.IsAny<Func<ListingReportDocument, bool>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetModerationQueueAsync_UsesQueryDefinitionAndOrdersFifo()
    {
        var containerMock = new Mock<ICosmosContainer<ListingReportDocument>>(MockBehavior.Strict);
        var dbContextMock = new Mock<ICosmosDbContext>();
        dbContextMock.SetupGet(x => x.ListingReports).Returns(containerMock.Object);

        var newer = new ListingReportDocument
        {
            Id = "report:2",
            RoomId = "room-2",
            RoomTitleSnapshot = "Phòng mới",
            ReporterUserId = Guid.NewGuid(),
            Reason = (int)ReportReason.SuspectedScam,
            Status = (int)ReportStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var older = new ListingReportDocument
        {
            Id = "report:1",
            RoomId = "room-1",
            RoomTitleSnapshot = "Phòng cũ",
            ReporterUserId = Guid.NewGuid(),
            Reason = (int)ReportReason.InaccurateInfo,
            Status = (int)ReportStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
        };

        containerMock
            .Setup(c => c.QueryAsync(It.IsAny<QueryDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([newer, older]);

        var repo = new CosmosListingReportRepository(dbContextMock.Object);
        var (items, totalCount) = await repo.GetModerationQueueAsync(ReportStatus.Pending, page: 1, pageSize: 10);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(2));
            Assert.That(items.Select(i => i.Id), Is.EqualTo(new[] { "report:1", "report:2" }));
        });
    }

    [Test]
    public void UpdateAsync_WhenReplaceThrowsPreconditionFailed_ThrowsConcurrencyConflictException()
    {
        var containerMock = new Mock<ICosmosContainer<ListingReportDocument>>();
        var dbContextMock = new Mock<ICosmosDbContext>();
        dbContextMock.SetupGet(x => x.ListingReports).Returns(containerMock.Object);

        containerMock
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ListingReportDocument>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Precondition failed", HttpStatusCode.PreconditionFailed, 0, "act", 1.0));

        var report = ListingReport.Rehydrate(
            "report:1",
            "room-1",
            "Phòng 1",
            null,
            Guid.NewGuid(),
            ReportReason.AlreadyRented,
            null,
            ReportStatus.Resolved,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Đã xử lý",
            "etag-1");

        var repo = new CosmosListingReportRepository(dbContextMock.Object);
        Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo.UpdateAsync(report));
    }
}
