using Microsoft.Data.Sqlite;

namespace RoomToRentRadar.Api.Data;

public static class SqliteDatabaseSetup
{
    public const string DefaultConnectionString = "Data Source=App_Data/roomtorentradar.db";

    public static string NormalizeConnectionString(IConfiguration configuration, string contentRootPath)
    {
        var configured = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = DefaultConnectionString;
        }

        var connectionStringBuilder = new SqliteConnectionStringBuilder(configured);
        if (!Path.IsPathRooted(connectionStringBuilder.DataSource))
        {
            var absolutePath = Path.GetFullPath(
                Path.Combine(contentRootPath, connectionStringBuilder.DataSource));

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            connectionStringBuilder.DataSource = absolutePath;
        }

        return connectionStringBuilder.ToString();
    }

    public static async Task EnableWalAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
