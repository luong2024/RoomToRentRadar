using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace RoomToRentRadar.Datastore.Sqlite.Context;

/// <summary>
/// T00.6: applies embedded SQL migrations in ascending version order, tracking progress in
/// <c>PRAGMA user_version</c>. Each script runs in its own transaction.
/// </summary>
public static class SqliteMigrator
{
    public sealed record MigrationScript(int Version, string Name, string Sql);

    private static readonly Regex ScriptResourceNamePattern = new(
        @"^Migrations/(\d{4})_[a-z0-9_]+\.sql$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<MigrationScript> LoadEmbedded()
    {
        var assembly = typeof(SqliteMigrator).Assembly;
        var scripts = new List<MigrationScript>();
        var versions = new HashSet<int>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var match = ScriptResourceNamePattern.Match(resourceName);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' is not a valid migration name; expected 'Migrations/NNNN_name.sql'.");
            }

            var version = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!versions.Add(version))
            {
                throw new InvalidOperationException(
                    $"Duplicate migration number {version.ToString("D4", CultureInfo.InvariantCulture)} in embedded resources.");
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            scripts.Add(new MigrationScript(version, resourceName, reader.ReadToEnd()));
        }

        return scripts.OrderBy(script => script.Version).ToList();
    }

    public static IReadOnlyList<int> Migrate(SqliteConnection connection) => Migrate(connection, LoadEmbedded());

    public static IReadOnlyList<int> Migrate(SqliteConnection connection, IReadOnlyList<MigrationScript> scripts)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(scripts);

        var ordered = scripts.OrderBy(script => script.Version).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Version == ordered[i - 1].Version)
            {
                throw new InvalidOperationException(
                    $"Duplicate migration number {ordered[i].Version.ToString("D4", CultureInfo.InvariantCulture)} ('{ordered[i - 1].Name}' and '{ordered[i].Name}').");
            }
        }

        var currentVersion = ReadUserVersion(connection);
        var highestVersion = ordered.Count == 0 ? 0 : ordered[^1].Version;
        if (currentVersion > highestVersion)
        {
            throw new InvalidOperationException(
                $"Database user_version {currentVersion} is newer than the highest known migration {highestVersion}; use a newer application build instead of downgrading the database.");
        }

        if (currentVersion == 0 && HasUserTables(connection))
        {
            throw new InvalidOperationException(
                "Database contains tables but PRAGMA user_version is 0 (created before migrations existed). Delete the local roomtorentradar.db once and let the app recreate it.");
        }

        var applied = new List<int>();
        foreach (var script in ordered)
        {
            if (script.Version <= currentVersion)
            {
                continue;
            }

            ApplyScript(connection, script);
            applied.Add(script.Version);
        }

        return applied;
    }

    private static void ApplyScript(SqliteConnection connection, MigrationScript script)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = script.Sql;
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                // Version is a parsed int, so the PRAGMA value can never carry external text.
                command.CommandText = "PRAGMA user_version = " + script.Version.ToString(CultureInfo.InvariantCulture) + ";";
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            throw new InvalidOperationException(
                $"Migration {script.Version.ToString("D4", CultureInfo.InvariantCulture)} ('{script.Name}') failed: {exception.Message}", exception);
        }
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static bool HasUserTables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }
}