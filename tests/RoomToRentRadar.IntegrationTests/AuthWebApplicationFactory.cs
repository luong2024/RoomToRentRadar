using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.IntegrationTests.TestControllers;

namespace RoomToRentRadar.IntegrationTests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-hmac-sha256-secret-key-at-least-32-bytes";
    public const string TestIssuer = "RoomToRentRadar.Api";
    public const string TestAudience = "RoomToRentRadar.Client";

    private readonly int _authPermitLimit;

    public StubGoogleIdentityVerifier GoogleVerifier { get; } = new();

    public CosmosDbContext CosmosDb { get; } = CosmosDbContext.CreateInMemory();

    public AuthWebApplicationFactory(int authPermitLimit = 100)
    {
        _authPermitLimit = authPermitLimit;
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
                ["RateLimiting:AuthWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICosmosDbContext>();
            services.AddSingleton<ICosmosDbContext>(CosmosDb);

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
