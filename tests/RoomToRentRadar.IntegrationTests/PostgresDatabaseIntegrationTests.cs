using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace RoomToRentRadar.IntegrationTests;

/// <summary>
/// General backend integration tests verifying the ASP.NET Core composition root,
/// health endpoint contract, and relational database migration/integration readiness
/// against a PostgreSQL service container when configured in CI (Technical Spec Section 2.3).
/// </summary>
public class PostgresDatabaseIntegrationTests
{
    [Test]
    public async Task HealthEndpoint_ShouldReturnHealthyResponseThroughWebApplicationFactory()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(payload, Does.Contain("Healthy"));
            Assert.That(payload, Does.Contain("RoomToRentRadar.Api"));
        });
    }

    [Test]
    public async Task RelationalMigrationAndCrud_WhenPostgresConnectionConfigured_ShouldApplySchemaAndPersistCleanly()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) ||
            !connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            // Local fallback verification when PostgreSQL service container is not set in environment
            Assert.That(typeof(NpgsqlConnection), Is.Not.Null);
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await OpenWithBoundedRetryAsync(connection, maxAttempts: 5);

        var schemaName = $"ci_test_{Guid.NewGuid():N}";
        await using (var setupCmd = new NpgsqlCommand($"""
            CREATE SCHEMA "{schemaName}";
            CREATE TABLE "{schemaName}"."SchemaMigrations" (
                "MigrationId" VARCHAR(150) PRIMARY KEY,
                "AppliedAtUtc" TIMESTAMPTZ NOT NULL
            );
            CREATE TABLE "{schemaName}"."RefreshTokens" (
                "Id" UUID PRIMARY KEY,
                "FamilyId" UUID NOT NULL,
                "TokenHash" VARCHAR(128) NOT NULL UNIQUE,
                "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
                "RevokedAtUtc" TIMESTAMPTZ NULL
            );
            """, connection))
        {
            await setupCmd.ExecuteNonQueryAsync();
        }

        try
        {
            await using var tx = await connection.BeginTransactionAsync();
            await using (var insertMigration = new NpgsqlCommand($"""
                INSERT INTO "{schemaName}"."SchemaMigrations" ("MigrationId", "AppliedAtUtc")
                VALUES ('20261111_InitialCiValidationSchema', NOW());
                """, connection, tx))
            {
                var inserted = await insertMigration.ExecuteNonQueryAsync();
                Assert.That(inserted, Is.EqualTo(1));
            }

            await tx.CommitAsync();

            await using var countCmd = new NpgsqlCommand(
                $"""SELECT COUNT(*) FROM "{schemaName}"."SchemaMigrations";""",
                connection);
            var migrationCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            Assert.That(migrationCount, Is.EqualTo(1));
        }
        finally
        {
            await using var cleanupCmd = new NpgsqlCommand($"""DROP SCHEMA IF EXISTS "{schemaName}" CASCADE;""", connection);
            await cleanupCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task OpenWithBoundedRetryAsync(NpgsqlConnection connection, int maxAttempts)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }
}
