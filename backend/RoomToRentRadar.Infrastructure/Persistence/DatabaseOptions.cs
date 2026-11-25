namespace RoomToRentRadar.Data
{
    /// <summary>
    /// Platform-agnostic database configuration injected into <see cref="RentalDatabaseService"/>.
    /// In the MAUI app, the connection string points to <c>FileSystem.AppDataDirectory</c>;
    /// in unit tests, it can point to an in-memory or temporary SQLite database.
    /// </summary>
    public sealed record DatabaseOptions(string ConnectionString);
}
