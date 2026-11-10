namespace RoomToRentRadar.Datastore.Sqlite.Configurations;

public sealed class SqliteSettings
{
    public const string SectionName = "Sqlite";

    public string ConnectionString { get; set; } = "Data Source=roomtorentradar.db";
}

