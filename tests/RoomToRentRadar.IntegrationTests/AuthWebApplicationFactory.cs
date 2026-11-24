using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;
using RoomToRentRadar.IntegrationTests.TestControllers;

namespace RoomToRentRadar.IntegrationTests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-hmac-sha256-secret-key-at-least-32-bytes";
    public const string TestIssuer = "RoomToRentRadar.Api";
    public const string TestAudience = "RoomToRentRadar.Client";

    private readonly int _authPermitLimit;
    private readonly int _routingPermitLimit;

    public StubGoogleIdentityVerifier GoogleVerifier { get; } = new();

    public CosmosDbContext CosmosDb { get; } = CosmosDbContext.CreateInMemory();

    public InMemoryRoomRepository RoomRepository { get; } = new();

    public InMemoryRoomListingRepository RoomListingRepository { get; } = new();

    public StubRoutingProvider RoutingProvider { get; } = new();

    public FakeListingImageStorageService ListingImageStorage { get; } = new();

    public AuthWebApplicationFactory(int authPermitLimit = 100, int routingPermitLimit = 100)
    {
        _authPermitLimit = authPermitLimit;
        _routingPermitLimit = routingPermitLimit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["Jwt:ClockSkewSeconds"] = "0",
                ["CosmosDb:AccountEndpoint"] = "in-memory",
                ["CosmosDb:DatabaseName"] = "RoomToRentRadarDb",
                ["GoogleAuth:ClientId"] = "test-google-client-id.apps.googleusercontent.com",
                ["RateLimiting:AuthPermitLimit"] = _authPermitLimit.ToString(),
                ["RateLimiting:AuthWindowSeconds"] = "60",
                ["RateLimiting:RoutingPermitLimit"] = _routingPermitLimit.ToString(),
                ["RateLimiting:RoutingWindowSeconds"] = "60",
                ["KycStorage:AccountName"] = "devstoreaccount1",
                ["KycStorage:AccountKey"] = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==",
                ["KycStorage:ContainerName"] = "kyc",
                ["KycStorage:AllowedHosts"] = "storage.roomtorentradar.vn,127.0.0.1,localhost,devstoreaccount1.blob.core.windows.net"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICosmosDbContext>();
            services.AddSingleton<ICosmosDbContext>(CosmosDb);

            services.RemoveAll<InMemoryRoomRepository>();
            services.AddSingleton(RoomRepository);
            services.RemoveAll<IRoomRepository>();
            services.AddSingleton<IRoomRepository>(RoomRepository);

            services.RemoveAll<IRoomListingRepository>();
            services.AddSingleton<IRoomListingRepository>(RoomListingRepository);
            services.RemoveAll<IRoomListingVisibilityWriter>();
            services.AddSingleton<IRoomListingVisibilityWriter>(RoomListingRepository);
            services.RemoveAll<ICampusRepository>();
            services.AddSingleton<ICampusRepository, IntegrationCampusRepository>();

            services.RemoveAll<IListingImageStorageService>();
            services.AddSingleton<IListingImageStorageService>(ListingImageStorage);

            services.RemoveAll<IRoutingProvider>();
            services.AddSingleton<IRoutingProvider>(RoutingProvider);

            services.RemoveAll<IGoogleIdentityVerifier>();
            services.AddSingleton<IGoogleIdentityVerifier>(GoogleVerifier);

            services
                .AddControllers()
                .AddApplicationPart(typeof(RbacProbeController).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            CosmosDb.Dispose();
        }
    }
}

public sealed class InMemoryRoomListingRepository : IRoomListingRepository, IRoomListingVisibilityWriter
{
    private readonly Dictionary<string, RoomListing> _listings = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<string>> HideAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (!_listings.TryGetValue(roomId, out var listing))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
        listing.UpdateStatus(ListingRoomStatus.Hidden);
        listing.UpdateVerificationStatus(VerificationStatus.Unverified);
        return Task.FromResult<IReadOnlyList<string>>([listing.CampusId]);
    }

    public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
    {
        _listings[roomListing.Id] = roomListing;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string roomId, string campusId, CancellationToken cancellationToken = default)
    {
        _listings.Remove(roomId);
        return Task.CompletedTask;
    }

    public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
        string campusId,
        GeoLocation campusLocation,
        int page,
        int pageSize,
        string sortBy,
        bool descending,
        CancellationToken cancellationToken = default)
    {
        var matching = _listings.Values
            .Where(listing => listing.CampusId == campusId && listing.IsDisplayable)
            .ToList();
        return Task.FromResult(new PagedResult<RoomListing>(matching, page, pageSize, matching.Count));
    }
}

public sealed class IntegrationCampusRepository : ICampusRepository
{
    private static readonly Campus Campus = new(
        "campus-ictu", "ICTU", "ICTU", new GeoLocation(21.5852, 105.8074), "Integration test campus");

    public Task<Campus?> GetByIdAsync(string campusId, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Equals(campusId, Campus.Id, StringComparison.Ordinal) ? Campus : null);

    public Task AddAsync(Campus campus, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class FakeListingImageStorageService : IListingImageStorageService
{
    public List<string> StoredUrls { get; } = [];
    public List<string> DeletedUrls { get; } = [];

    public Task<string> StoreAsync(Guid ownerId, string listingId, Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var url = $"https://listing-images.test/{ownerId:D}/{listingId}/{Guid.NewGuid():N}.png";
        StoredUrls.Add(url);
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        DeletedUrls.Add(imageUrl);
        return Task.CompletedTask;
    }
}

public sealed class StubRoutingProvider : IRoutingProvider
{
    public RoutingProviderResult? Result { get; set; } = new(0, 0, Array.Empty<RouteCoordinate>());

    public Exception? ExceptionToThrow { get; set; }

    public Task<RoutingProviderResult?> GetRouteAsync(
        GeoLocation origin,
        GeoLocation destination,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ExceptionToThrow is not null)
        {
            return Task.FromException<RoutingProviderResult?>(ExceptionToThrow);
        }

        return Task.FromResult(Result);
    }
}

public sealed class StubGoogleIdentityVerifier : IGoogleIdentityVerifier
{
    private readonly Dictionary<string, VerifiedGoogleIdentity> _tokens = new(StringComparer.Ordinal);

    public void RegisterValidToken(string idToken, string subject, string email, bool emailVerified = true)
    {
        _tokens[idToken] = new VerifiedGoogleIdentity(subject, email, emailVerified);
    }

    public Task<VerifiedGoogleIdentity> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(idToken, out var identity))
        {
            return Task.FromResult(identity);
        }

        throw new RoomToRentRadar.Application.Exceptions.InvalidGoogleTokenException();
    }
}
