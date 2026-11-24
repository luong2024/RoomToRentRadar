namespace RoomToRentRadar.Adapters.Tests.Inbound;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Application.Ports.Inbound;

[TestFixture]
public sealed class LandlordListingsControllerTests
{
    [Test]
    public async Task Create_AuthenticatedLandlord_UsesIdentityAndPreservesExistingFormContract()
    {
        var ownerId = Guid.NewGuid();
        var create = new CaptureCreateUseCase();
        var controller = CreateController(create, ownerId);
        var form = new ListingFormRequest
        {
            Title = "Listing title",
            Address = "10 Main Street",
            MonthlyRentVnd = 2500000,
            DepositVnd = 0,
            AreaM2 = 22,
            RoomType = "BoardingRoom"
        };

        var result = await controller.Create(form, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<CreatedResult>());
            Assert.That(create.OwnerId, Is.EqualTo(ownerId));
            Assert.That(create.Request!.Title, Is.EqualTo("Listing title"));
            Assert.That(create.Request.DepositVnd, Is.Zero);
            Assert.That(create.Request.RoomType, Is.EqualTo("BoardingRoom"));
        });
    }

    [Test]
    public async Task Create_MissingTrustedIdentity_ReturnsUnauthorizedWithoutCallingUseCase()
    {
        var create = new CaptureCreateUseCase();
        var controller = CreateController(create, null);

        var result = await controller.Create(new ListingFormRequest(), CancellationToken.None);

        Assert.That(result, Is.TypeOf<UnauthorizedResult>());
        Assert.That(create.WasCalled, Is.False);
    }

    [Test]
    public void Controller_RequiresLandlordOnlyPolicy()
    {
        var authorization = typeof(LandlordListingsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.That(authorization.Policy, Is.EqualTo(AuthorizationPolicies.LandlordOnly));
    }

    private static LandlordListingsController CreateController(CaptureCreateUseCase create, Guid? ownerId)
    {
        var controller = new LandlordListingsController(create, new NoOpUpdateUseCase(), new NoOpUnpublishUseCase());
        var claims = ownerId.HasValue ? new[] { new Claim(ClaimTypes.NameIdentifier, ownerId.Value.ToString("D")) } : [];
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        return controller;
    }

    private sealed class CaptureCreateUseCase : ICreateListingUseCase
    {
        public Guid OwnerId { get; private set; }
        public ListingWriteRequest? Request { get; private set; }
        public bool WasCalled => Request is not null;

        public Task<ListingWriteResult> ExecuteAsync(Guid ownerId, ListingWriteRequest request, CancellationToken cancellationToken = default)
        {
            OwnerId = ownerId;
            Request = request;
            return Task.FromResult(new ListingWriteResult("listing-id", "Pending", []));
        }
    }

    private sealed class NoOpUpdateUseCase : IUpdateListingUseCase
    {
        public Task<ListingWriteResult> ExecuteAsync(Guid ownerId, string listingId, ListingWriteRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ListingWriteResult(listingId, "Pending", []));
    }

    private sealed class NoOpUnpublishUseCase : IUnpublishListingUseCase
    {
        public Task ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
