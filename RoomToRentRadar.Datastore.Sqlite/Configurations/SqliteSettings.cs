namespace RoomToRentRadar.Datastore.Sqlite.Configurations;

public sealed class SqliteSettings
{
    public const string SectionName = "Sqlite";

    public string ConnectionString { get; set; } = "Data Source=roomtorentradar.db";

    /// <summary>
    /// H4: demo rooms are seeded only when this flag is on (Development/test).
    /// <c>Campuses</c> is reference data and is always seeded.
    /// </summary>
    public bool SeedDemoRooms { get; set; }
}
