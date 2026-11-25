using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.SqliteDataAccess;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class ListingsApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "rtrr-test-signing-key-0123456789abcdef";

    private const string CreateNoUpdateTriggerSql =
        """
        CREATE TRIGGER "trg_moderation_audit_logs_no_update"
        BEFORE UPDATE ON "moderation_audit_logs"
        BEGIN
            SELECT RAISE(ABORT, 'moderation audit log is append-only');
        END;
        """;

    private const string CreateNoDeleteTriggerSql =
        """
        CREATE TRIGGER "trg_moderation_audit_logs_no_delete"
        BEFORE DELETE ON "moderation_audit_logs"
        BEGIN
            SELECT RAISE(ABORT, 'moderation audit log is append-only');
        END;
        """;
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "RoomToRentRadarTests",
        Guid.NewGuid().ToString("N"));

    private readonly Action<IServiceCollection>? _configureTestServices;

    public ListingsApiFactory(Action<IServiceCollection>? configureTestServices = null)
    {
        _configureTestServices = configureTestServices;
    }

    public QueryCountingInterceptor QueryCounter { get; } = new();

    public RecordingEmailSender EmailSink { get; } = new();

    public FixedOtpGenerator OtpCodes { get; } = new();

    public string DatabasePath => Path.Combine(_directory, "test.db");

    public string MediaRootPath => Path.Combine(_directory, "media");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DatabasePath}");
        builder.UseSetting("Storage:PhotoRootPath", MediaRootPath);
        builder.UseSetting("Alerts:Enabled", "false");
        builder.UseSetting("Auth:Token:SigningKey", TestSigningKey);
        builder.UseSetting("Auth:Email:Smtp:Host", "smtp.test.invalid");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<DbCommandInterceptor>(QueryCounter);
            // Auth limiter partitions are per-IP and shared across a fixture run.
            services.Configure<AuthOptions>(options => options.RateLimitPerMinute = 1000);
            services.AddSingleton<IOtpGenerator>(OtpCodes);
            services.AddSingleton<IEmailSender>(EmailSink);
            _configureTestServices?.Invoke(services);
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

        // The audit table is append-only at the database level, so cleanup has to
        // drop the guards before deleting rows and restore them afterwards.
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_update\";");
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_delete\";");

        await dbContext.ModerationAuditLogs.ExecuteDeleteAsync();
        await dbContext.ListingReports.ExecuteDeleteAsync();
        await dbContext.ListingFavourites.ExecuteDeleteAsync();
        await dbContext.Notifications.ExecuteDeleteAsync();
        await dbContext.SavedSearchAlertLogs.ExecuteDeleteAsync();
        await dbContext.SavedSearches.ExecuteDeleteAsync();
        await dbContext.RegistrationChallenges.ExecuteDeleteAsync();
        await dbContext.AuthSessions.ExecuteDeleteAsync();
        await dbContext.Users.ExecuteDeleteAsync();
        await dbContext.ListingHouseRuleVersions.ExecuteDeleteAsync();
        await dbContext.ListingHouseRules.ExecuteDeleteAsync();
        await dbContext.ListingAmenities.ExecuteDeleteAsync();
        await dbContext.ListingRules.ExecuteDeleteAsync();
        await dbContext.ListingPhotos.ExecuteDeleteAsync();
        await dbContext.Listings.ExecuteDeleteAsync();

        await dbContext.Database.ExecuteSqlRawAsync(CreateNoUpdateTriggerSql);
        await dbContext.Database.ExecuteSqlRawAsync(CreateNoDeleteTriggerSql);

        QueryCounter.Reset();
        EmailSink.Reset();
        OtpCodes.Reset();
        ClearMediaDirectory();
    }

    private void ClearMediaDirectory()
    {
        if (!Directory.Exists(MediaRootPath))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(MediaRootPath))
        {
            Directory.Delete(directory, recursive: true);
        }

        foreach (var file in Directory.GetFiles(MediaRootPath))
        {
            File.Delete(file);
        }
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
