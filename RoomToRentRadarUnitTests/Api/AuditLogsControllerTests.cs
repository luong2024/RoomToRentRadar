using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Contracts.Responses.Audit;
using RoomToRentRadar.Ports.Inbound.UseCases.Audit;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class AuditLogsControllerTests
{
    private IGetAuditLogsUseCase _useCase = null!;
    private AuditLogsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _useCase = Substitute.For<IGetAuditLogsUseCase>();
        _controller = new AuditLogsController(_useCase);
    }

    [Test]
    public void Controller_HasAdminPolicyAuthorizationAttribute()
    {
        var authAttr = typeof(AuditLogsController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(authAttr, Is.Not.Null, "Controller must have [Authorize] attribute.");
            Assert.That(authAttr!.Policy, Is.EqualTo(AuthorizationPolicies.AdminPolicy));
        });
    }

    [Test]
    public async Task GetAuditLogs_DelegatesToUseCase_ReturnsOkWithPagedResult()
    {
        var request = new AuditLogQueryRequest(UserId: "admin-01");
        var expectedDto = new AuditLogDto(
            Id: "log-1",
            UserId: "admin-01",
            UserEmail: "admin@ictu.edu.vn",
            Role: "Admin",
            Action: "LANDLORD_APPROVED",
            EntityType: "User",
            EntityId: "landlord-2",
            DetailsJson: null,
            IpAddress: "127.0.0.1",
            UserAgent: "Browser",
            StatusCode: 200,
            DurationMs: 15,
            IsSuccess: true,
            ErrorMessage: null,
            TimestampUtc: DateTime.UtcNow);

        var pagedResult = new PagedResult<AuditLogDto>(new[] { expectedDto }, 1, 1, 50);
        _useCase.ExecuteAsync(request, Arg.Any<CancellationToken>()).Returns(pagedResult);

        var response = await _controller.GetAuditLogs(request);
        var okResult = response.Result as OkObjectResult;

        Assert.Multiple(() =>
        {
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.StatusCode, Is.EqualTo(200));
            Assert.That(okResult.Value, Is.EqualTo(pagedResult));
        });
    }
}

