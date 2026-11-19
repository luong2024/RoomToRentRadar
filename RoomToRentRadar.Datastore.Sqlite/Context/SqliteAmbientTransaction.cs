using Microsoft.Data.Sqlite;

namespace RoomToRentRadar.Datastore.Sqlite.Context;

/// <summary>
/// Ambient unit-of-work state for the SQLite datastore (US08 create/update listing). While
/// <c>SqliteUnitOfWork.ExecuteAsync</c> runs its action, every repository in the same async flow sees
/// one connection and one transaction here and joins it instead of opening its own — the listing row,
/// its children and the photo attach/remove writes then commit or roll back together.
/// </summary>
internal static class SqliteAmbientTransaction
{
    private static readonly AsyncLocal<AmbientScope?> Ambient = new();

    public static AmbientScope? Current => Ambient.Value;

    public static IDisposable Push(SqliteConnection connection, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var previous = Ambient.Value;
        Ambient.Value = new AmbientScope(connection, transaction);
        return new PopScope(previous);
    }

    /// <summary>Connection + transaction a repository must reuse while a unit of work is active.</summary>
    internal sealed record AmbientScope(SqliteConnection Connection, SqliteTransaction Transaction);

    private sealed class PopScope(AmbientScope? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            // Restore the previous value instead of null so a joined (nested) caller cannot clear the
            // outer scope; the outermost ExecuteAsync owns the real transaction.
            Ambient.Value = previous;
        }
    }
}
