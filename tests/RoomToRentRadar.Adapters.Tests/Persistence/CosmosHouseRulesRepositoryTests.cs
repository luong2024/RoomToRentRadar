using System.Net;
using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class CosmosHouseRulesRepositoryTests
{
    [Test]
    public async Task GetRevisionsByRoomIdAsync_UsesQueryDefinition_AndNeverUsesInMemoryPredicate()
    {
        var containerMock = new Mock<ICosmosContainer<RoomHouseRulesDocument>>(MockBehavior.Strict);
        var dbContextMock = new Mock<ICosmosDbContext>();
        dbContextMock.SetupGet(x => x.HouseRules).Returns(containerMock.Object);

        QueryDefinition? capturedQuery = null;
        var policy = HouseRulesPolicy.Create("22:00", "No overnight guests", "Electric only", "1 bike");
        var rev1 = new HouseRulesRevision("room-1", 1, policy, "v1", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-10));
        var rev2 = new HouseRulesRevision("room-1", 2, policy, "v2", Guid.NewGuid(), DateTimeOffset.UtcNow);

        containerMock
            .Setup(c => c.QueryAsync(It.IsAny<QueryDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<QueryDefinition, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync([
                RoomHouseRulesDocument.FromRevisionDomain(rev1),
                RoomHouseRulesDocument.FromRevisionDomain(rev2)
            ]);

        var repository = new CosmosHouseRulesRepository(dbContextMock.Object);
        var results = await repository.GetRevisionsByRoomIdAsync("  room-1  ");

        Assert.Multiple(() =>
        {
            Assert.That(capturedQuery, Is.Not.Null);
            Assert.That(capturedQuery!.QueryText, Does.Contain("c.roomId = @roomId"));
            Assert.That(capturedQuery.QueryText, Does.Contain("c.documentType = @documentType"));
            Assert.That(results.Select(r => r.Version), Is.EqualTo(new[] { 2, 1 }));
        });

        containerMock.Verify(
            c => c.QueryAsync(It.IsAny<Func<RoomHouseRulesDocument, bool>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task SaveAsync_ExecutesSingleTransactionalBatchWithCurrentAndRevision()
    {
        var containerMock = new Mock<ICosmosContainer<RoomHouseRulesDocument>>(MockBehavior.Strict);
        var dbContextMock = new Mock<ICosmosDbContext>();
        dbContextMock.SetupGet(x => x.HouseRules).Returns(containerMock.Object);

        string? capturedPartitionKey = null;
        IReadOnlyList<CosmosBatchOperation<RoomHouseRulesDocument>>? capturedOps = null;

        containerMock
            .Setup(c => c.ExecuteBatchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CosmosBatchOperation<RoomHouseRulesDocument>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<CosmosBatchOperation<RoomHouseRulesDocument>>, CancellationToken>((pk, ops, _) =>
            {
                capturedPartitionKey = pk;
                capturedOps = ops;
            })
            .Returns(Task.CompletedTask);

        var landlordId = Guid.NewGuid();
        var policy = HouseRulesPolicy.Create("22:00", "No guests", "Electric only", "1 bike");
        var existing = RoomHouseRules.Rehydrate(
            RoomHouseRules.BuildId("room-42"),
            "room-42",
            landlordId,
            currentVersion: 1,
            policy,
            DateTimeOffset.UtcNow.AddDays(-1),
            landlordId,
            etag: "etag-v1");

        var newPolicy = HouseRulesPolicy.Create("23:00", "Until 22:00", "Electric only", "1 bike");
        var rev2 = existing.UpdatePolicy(newPolicy, landlordId, isAdmin: false, "Updated", DateTimeOffset.UtcNow);

        var repository = new CosmosHouseRulesRepository(dbContextMock.Object);
        await repository.SaveAsync(existing, rev2);

        Assert.Multiple(() =>
        {
            Assert.That(capturedPartitionKey, Is.EqualTo("room-42"));
            Assert.That(capturedOps, Has.Count.EqualTo(2));
            Assert.That(capturedOps![0].Type, Is.EqualTo(CosmosBatchOperationType.Replace));
            Assert.That(capturedOps[0].IfMatchEtag, Is.EqualTo("etag-v1"));
            Assert.That(capturedOps[0].Item.DocumentType, Is.EqualTo(RoomHouseRulesDocument.CurrentDocumentType));
            Assert.That(capturedOps[1].Type, Is.EqualTo(CosmosBatchOperationType.Create));
            Assert.That(capturedOps[1].Item.DocumentType, Is.EqualTo(RoomHouseRulesDocument.RevisionDocumentType));
            Assert.That(capturedOps[1].Item.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public void SaveAsync_WhenBatchThrowsPreconditionFailed_ThrowsConcurrencyConflictException()
    {
        var containerMock = new Mock<ICosmosContainer<RoomHouseRulesDocument>>();
        var dbContextMock = new Mock<ICosmosDbContext>();
        dbContextMock.SetupGet(x => x.HouseRules).Returns(containerMock.Object);

        containerMock
            .Setup(c => c.ExecuteBatchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CosmosBatchOperation<RoomHouseRulesDocument>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("ETag mismatch", HttpStatusCode.PreconditionFailed, 0, "act", 1.0));

        var landlordId = Guid.NewGuid();
        var policy = HouseRulesPolicy.Create("22:00", "No guests", "Electric only", "1 bike");
        var (rules, rev1) = RoomHouseRules.CreateInitial("room-99", landlordId, policy, landlordId, false, null, DateTimeOffset.UtcNow);

        var repository = new CosmosHouseRulesRepository(dbContextMock.Object);
        Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.SaveAsync(rules, rev1));
    }

    [Test]
    public async Task InMemoryContainer_ExecuteBatchAsync_WhenSecondOperationFails_RollsBackFirstOperation()
    {
        var dbContext = CosmosDbContext.CreateInMemory();
        var repository = new CosmosHouseRulesRepository(dbContext);

        var landlordId = Guid.NewGuid();
        var policy1 = HouseRulesPolicy.Create("22:00", "No guests", "Electric only", "1 bike");
        var (rules, rev1) = RoomHouseRules.CreateInitial("room-atomic", landlordId, policy1, landlordId, false, "v1", DateTimeOffset.UtcNow);
        await repository.SaveAsync(rules, rev1);

        var loaded = await repository.GetByRoomIdAsync("room-atomic");
        Assert.That(loaded, Is.Not.Null);

        // Force a duplicate revision v1 in a batch that tries to replace current -> v2 and create duplicate rev1
        var policy2 = HouseRulesPolicy.Create("23:00", "Guests until 21:00", "Electric only", "1 bike");
        var staleRevision = new HouseRulesRevision("room-atomic", 1, policy2, "duplicate v1", landlordId, DateTimeOffset.UtcNow);
        loaded!.UpdatePolicy(policy2, landlordId, false, "v2", DateTimeOffset.UtcNow);

        Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.SaveAsync(loaded, staleRevision));

        var afterFailure = await repository.GetByRoomIdAsync("room-atomic");
        Assert.That(afterFailure!.CurrentVersion, Is.EqualTo(1));
        Assert.That(afterFailure.CurrentPolicy.QuietHours, Is.EqualTo("22:00"));
    }
}
