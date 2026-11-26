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

    /// <summary>
    /// T00.10: seeds the extended 30-room catalog (8 core + 22 extra) and child tables
    /// (amenities, safety features, house rules). Effective only when <see cref="SeedDemoRooms"/> is true.
    /// </summary>
    public bool SeedExtendedDemoData { get; set; }

    /// <summary>
    /// Pre-hashed accounts loaded from <c>Admin:SeedAccounts</c> (and in Development
    /// <c>DemoData:Accounts</c>) to be inserted idempotently on schema initialization.
    /// </summary>
    public IReadOnlyList<SeedAccount> SeedAccounts { get; set; } = Array.Empty<SeedAccount>();

    /// <summary>
    /// Development photos attached to demo rooms that have none. Effective only together with
    /// <see cref="SeedDemoRooms"/> and <see cref="SeedExtendedDemoData"/>.
    /// </summary>
    public IReadOnlyList<SeedPhoto> SeedPhotos { get; set; } = Array.Empty<SeedPhoto>();
}
