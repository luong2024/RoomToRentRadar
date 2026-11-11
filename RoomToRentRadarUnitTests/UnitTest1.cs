using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.DependencyInjection;

namespace RoomToRentRadarUnitTests;

/// <summary>
/// Validates MAUI Core Dependency Injection container registrations using strict
/// ValidateOnBuild and ValidateScopes options (Technical Spec Section 4.1).
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class MauiCoreServiceRegistrationTests
{
    [Test]
    public async Task Core_Services_And_ViewModels_Should_Resolve_With_ValidateOnBuild_And_ValidateScopes()
    {
        var services = new ServiceCollection();

        // Register only platform-agnostic core services, HTTP clients, and ViewModels.
        // Do NOT register Shell/Page/Handler types that require platform UI runtime on Linux CI.
        services.AddRoomToRentRadarCore();
        services.AddRoomToRentRadarViewModels();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        await using var scope = provider.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<HomeViewModel>();
        var apiClient = scope.ServiceProvider.GetRequiredService<IApiClient>();

        Assert.Multiple(() =>
        {
            Assert.That(vm, Is.Not.Null);
            Assert.That(vm.Title, Is.EqualTo("RoomToRentRadar"));
            Assert.That(vm.Subtitle, Is.EqualTo("Student Room Rental Radar — ICTU"));
            Assert.That(apiClient, Is.Not.Null);
        });

        vm.Title = "Updated Radar Title";
        Assert.That(vm.Title, Is.EqualTo("Updated Radar Title"));
    }

    [Test]
    public async Task ApiClient_GetAsync_ShouldDeserializeJsonPayloadCorrectly()
    {
        using var handler = new StubHttpMessageHandler("{\"status\":\"Healthy\"}");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.roomtorentradar.local")
        };

        var client = new ApiClient(httpClient);
        var response = await client.GetAsync<HealthPayload>("/api/health");

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Healthy"));
    }

    [Test]
    public void BuildServiceProvider_WhenSingletonCapturesScopedService_ShouldThrowOnBuild()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddSingleton<CaptiveSingletonConsumer>();

        Assert.Throws<AggregateException>(() =>
        {
            using var _ = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        });
    }

    private sealed record HealthPayload(string Status);

    private sealed class ScopedDependency;

    private sealed class CaptiveSingletonConsumer(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}

/// <summary>
/// File-based SQLite WAL concurrency integration tests for the offline cache layer
/// (Technical Spec Section 4.2). Uses a real temporary .db3 file on disk rather than :memory:.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class SqliteWalConcurrencyTests
{
    private string _dbPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        SQLitePCL.Batteries_V2.Init();
        _dbPath = Path.Combine(Path.GetTempPath(), $"roomradar_wal_{Guid.NewGuid():N}.db3");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        DeleteFileIfExists(_dbPath);
        DeleteFileIfExists($"{_dbPath}-wal");
        DeleteFileIfExists($"{_dbPath}-shm");
    }

    [Test]
    public async Task SqliteFileDatabase_WhenConcurrentReadersAndSerializedWritersWithWal_ShouldPreserveDataIntegrity()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString();

        // 1. Initialize schema and verify WAL mode on the physical .db3 file
        await using (var initConn = await OpenConfiguredConnectionAsync(connectionString))
        {
            await using var modeCmd = initConn.CreateCommand();
            modeCmd.CommandText = "PRAGMA journal_mode;";
            var journalMode = (string?)await modeCmd.ExecuteScalarAsync();
            Assert.That(journalMode?.ToLowerInvariant(), Is.EqualTo("wal"));

            await using var schemaCmd = initConn.CreateCommand();
            schemaCmd.CommandText = """
                CREATE TABLE LandlordCache (
                    LandlordId TEXT PRIMARY KEY NOT NULL,
                    DisplayName TEXT NOT NULL
                );

                CREATE TABLE RoomListingCache (
                    ListingId TEXT PRIMARY KEY NOT NULL,
                    LandlordId TEXT NOT NULL,
                    RoomCode TEXT NOT NULL UNIQUE,
                    MonthlyRentVnd INTEGER NOT NULL,
                    UpdatedAtUtcTicks INTEGER NOT NULL,
                    FOREIGN KEY (LandlordId) REFERENCES LandlordCache(LandlordId) ON DELETE CASCADE
                );

                INSERT INTO LandlordCache (LandlordId, DisplayName)
                VALUES ('landlord-ictu-01', 'Khu Tro Sinh Vien ICTU');
                """;
            await schemaCmd.ExecuteNonQueryAsync();
        }

        Assert.That(File.Exists(_dbPath), Is.True, "Physical .db3 file must exist on disk.");

        const int writerTaskCount = 6;
        const int itemsPerWriter = 10;
        const int expectedTotalRecords = writerTaskCount * itemsPerWriter;
        const int readerTaskCount = 4;
        const int readsPerReader = 25;

        // App-level single-writer gate + bounded retry policy for SQLITE_BUSY/SQLITE_LOCKED
        using var writeGate = new SemaphoreSlim(1, 1);
        using var startSignal = new ManualResetEventSlim(false);
        var observedReadSnapshots = new System.Collections.Concurrent.ConcurrentBag<int>();

        var writerTasks = Enumerable.Range(0, writerTaskCount).Select(writerIndex => Task.Run(async () =>
        {
            startSignal.Wait();
            for (var itemIndex = 0; itemIndex < itemsPerWriter; itemIndex++)
            {
                var listingId = $"listing-{writerIndex:D2}-{itemIndex:D2}";
                var roomCode = $"ICTU-R{writerIndex:D2}-{itemIndex:D2}";
                var rent = 1_500_000L + (writerIndex * 100_000L) + (itemIndex * 10_000L);

                await ExecuteWriteWithRetryAsync(writeGate, async () =>
                {
                    await using var conn = await OpenConfiguredConnectionAsync(connectionString);
                    await using var tx = await conn.BeginTransactionAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = (SqliteTransaction)tx;
                    cmd.CommandText = """
                        INSERT INTO RoomListingCache (ListingId, LandlordId, RoomCode, MonthlyRentVnd, UpdatedAtUtcTicks)
                        VALUES ($id, 'landlord-ictu-01', $code, $rent, $ticks);
                        """;
                    cmd.Parameters.AddWithValue("$id", listingId);
                    cmd.Parameters.AddWithValue("$code", roomCode);
                    cmd.Parameters.AddWithValue("$rent", rent);
                    cmd.Parameters.AddWithValue("$ticks", DateTimeOffset.UtcNow.UtcTicks);
                    await cmd.ExecuteNonQueryAsync();
                    await tx.CommitAsync();
                });
            }
        })).ToArray();

        var readerTasks = Enumerable.Range(0, readerTaskCount).Select(_ => Task.Run(async () =>
        {
            startSignal.Wait();
            for (var readIteration = 0; readIteration < readsPerReader; readIteration++)
            {
                await using var conn = await OpenConfiguredConnectionAsync(connectionString);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT COUNT(*), COALESCE(MIN(MonthlyRentVnd), 1500000)
                    FROM RoomListingCache
                    WHERE LandlordId = 'landlord-ictu-01';
                    """;
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.That(await reader.ReadAsync(), Is.True);
                var count = reader.GetInt32(0);
                var minRent = reader.GetInt64(1);
                Assert.That(count, Is.InRange(0, expectedTotalRecords));
                Assert.That(minRent, Is.GreaterThanOrEqualTo(1_500_000L));
                observedReadSnapshots.Add(count);
                await Task.Delay(2);
            }
        })).ToArray();

        startSignal.Set();
        await Task.WhenAll(writerTasks.Concat(readerTasks));

        // Verify final record count, uniqueness, and foreign-key integrity
        await using (var verifyConn = await OpenConfiguredConnectionAsync(connectionString))
        {
            await using var countCmd = verifyConn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*), COUNT(DISTINCT RoomCode) FROM RoomListingCache;";
            await using var reader = await countCmd.ExecuteReaderAsync();
            Assert.That(await reader.ReadAsync(), Is.True);
            var totalRows = reader.GetInt32(0);
            var distinctCodes = reader.GetInt32(1);

            Assert.Multiple(() =>
            {
                Assert.That(totalRows, Is.EqualTo(expectedTotalRecords));
                Assert.That(distinctCodes, Is.EqualTo(expectedTotalRecords));
                Assert.That(observedReadSnapshots.Count, Is.EqualTo(readerTaskCount * readsPerReader));
            });

            await reader.CloseAsync();

            await using var fkCheckCmd = verifyConn.CreateCommand();
            fkCheckCmd.CommandText = "PRAGMA foreign_key_check;";
            var fkViolation = await fkCheckCmd.ExecuteScalarAsync();
            Assert.That(fkViolation, Is.Null, "Foreign key integrity check must report zero violations.");
        }
    }

    private static async Task<SqliteConnection> OpenConfiguredConnectionAsync(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;
            PRAGMA foreign_keys=ON;
            """;
        await pragmaCmd.ExecuteNonQueryAsync();
        return connection;
    }

    private static async Task ExecuteWriteWithRetryAsync(SemaphoreSlim writeGate, Func<Task> writeAction)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await writeGate.WaitAsync();
            try
            {
                await writeAction();
                return;
            }
            catch (SqliteException ex) when ((ex.SqliteErrorCode is 5 or 6) && attempt < maxAttempts)
            {
                await Task.Delay(20 * attempt);
            }
            finally
            {
                writeGate.Release();
            }
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
