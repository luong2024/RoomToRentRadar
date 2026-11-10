using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Adapters.Outbound.Persistence;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.IntegrationTests.TestControllers;

namespace RoomToRentRadar.IntegrationTests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-hmac-sha256-secret-key-at-least-32-bytes";
    public const string TestIssuer = "RoomToRentRadar.Api";
    public const string TestAudience = "RoomToRentRadar.Client";

    private readonly SqliteConnection _connection;
    private readonly int _authPermitLimit;

    public StubGoogleIdentityVerifier GoogleVerifier { get; } = new();

    public AuthWebApplicationFactory(int authPermitLimit = 100)
    {
        _authPermitLimit = authPermitLimit;
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
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
                ["GoogleAuth:ClientId"] = "test-google-client-id.apps.googleusercontent.com",
                ["RateLimiting:AuthPermitLimit"] = _authPermitLimit.ToString(),
                ["RateLimiting:AuthWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            services.RemoveAll<IGoogleIdentityVerifier>();
            services.AddSingleton<IGoogleIdentityVerifier>(GoogleVerifier);

            services
                .AddControllers()
                .AddApplicationPart(typeof(RbacProbeController).Assembly);

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
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

        throw new InvalidGoogleTokenException("Invalid Google ID token.");
    }
}
