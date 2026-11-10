using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class ListingsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "RoomToRentRadarTests",
        Guid.NewGuid().ToString("N"));

    public QueryCountingInterceptor QueryCounter { get; } = new();

    public string DatabasePath => Path.Combine(_directory, "test.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DatabasePath}");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<DbCommandInterceptor>(QueryCounter);
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.ListingAmenities.ExecuteDeleteAsync();
        await dbContext.ListingRules.ExecuteDeleteAsync();
        await dbContext.ListingPhotos.ExecuteDeleteAsync();
        await dbContext.Listings.ExecuteDeleteAsync();
        QueryCounter.Reset();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();

        for (var attempt = 0; attempt < 5 && Directory.Exists(_directory); attempt++)
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
