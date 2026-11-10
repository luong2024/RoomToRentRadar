using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SecurityAuditLoggerAdapterTests
{
    private IAuditLogRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IAuditLogRepository>();
    }

    [Test]
    public async Task LogLoginSucceeded_WritesTraceAndPersistsToRepository()
    {
        string? tracedMessage = null;
        var adapter = new SecurityAuditLoggerAdapter(
            auditLogRepository: _repository,
            sink: msg => tracedMessage = msg);

        adapter.LogLoginSucceeded("user-01", "student@ictu.edu.vn", "Student", "Local");

        // Allow fire-and-forget background task to execute
        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(tracedMessage, Does.Contain("LOGIN_SUCCEEDED"));
            Assert.That(tracedMessage, Does.Contain("student@ictu.edu.vn"));
        });

        await _repository.Received(1).InsertAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.UserId == "user-01" &&
                e.UserEmail == "student@ictu.edu.vn" &&
                e.Action == "LOGIN_SUCCEEDED" &&
                e.IsSuccess == true),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LogLoginFailed_PersistsFailedAuditEntry()
    {
        var adapter = new SecurityAuditLoggerAdapter(auditLogRepository: _repository);

        adapter.LogLoginFailed("unknown@ictu.edu.vn", "Invalid credentials");

        await Task.Delay(100);

        await _repository.Received(1).InsertAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.UserEmail == "unknown@ictu.edu.vn" &&
                e.Action == "LOGIN_FAILED" &&
                e.IsSuccess == false &&
                e.ErrorMessage == "Invalid credentials"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void PersistLogSafe_WhenRepositoryThrows_DoesNotThrowToCaller()
    {
        _repository.When(r => r.InsertAsync(Arg.Any<AuditLogEntry>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("DB error"));

        var adapter = new SecurityAuditLoggerAdapter(auditLogRepository: _repository);

        Assert.DoesNotThrow(() =>
        {
            adapter.LogAccountLockedOut("user-02", "locked@ictu.edu.vn", DateTime.UtcNow.AddMinutes(15));
        });
    }
}

