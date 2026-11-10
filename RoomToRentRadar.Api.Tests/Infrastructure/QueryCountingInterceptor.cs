using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class QueryCountingInterceptor : DbCommandInterceptor
{
    private int _listingsSelectCount;

    public int ListingsSelectCount => Volatile.Read(ref _listingsSelectCount);

    public void Reset() => Interlocked.Exchange(ref _listingsSelectCount, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        CountListingsSelect(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CountListingsSelect(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void CountListingsSelect(DbCommand command)
    {
        if (command.CommandText.Contains("FROM \"listings\"", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _listingsSelectCount);
        }
    }
}
