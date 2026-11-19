using Microsoft.Data.Sqlite;

namespace RoomToRentRadar.Datastore.Sqlite.Context;

/// <summary>
/// One connection a repository may use for the whole command. It joins the ambient unit of work when
/// one is active (shared connection + transaction, not disposed here) and otherwise opens a short-lived
/// connection from <see cref="SqliteDbContext"/> exactly as the repositories did before US08.
/// </summary>
internal readonly struct SqliteConnectionLease : IDisposable
{
    private readonly bool _disposeConnection;

    private SqliteConnectionLease(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        bool disposeConnection)
    {
        Connection = connection;
        Transaction = transaction;
        _disposeConnection = disposeConnection;
    }

    public SqliteConnection Connection { get; }

    /// <summary>Ambient transaction to assign to every command; null outside a unit of work.</summary>
    public SqliteTransaction? Transaction { get; }

    public SqliteCommand CreateCommand()
    {
        var command = Connection.CreateCommand();
        if (Transaction is not null)
        {
            command.Transaction = Transaction;
        }

        return command;
    }

    public static SqliteConnectionLease Acquire(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (SqliteAmbientTransaction.Current is { } ambient)
        {
            return new SqliteConnectionLease(ambient.Connection, ambient.Transaction, disposeConnection: false);
        }

        return new SqliteConnectionLease(
            dbContext.CreateOpenConnection(),
            transaction: null,
            disposeConnection: !dbContext.OwnsSharedConnection);
    }

    public void Dispose()
    {
        if (_disposeConnection)
        {
            Connection.Dispose();
        }
    }
}
