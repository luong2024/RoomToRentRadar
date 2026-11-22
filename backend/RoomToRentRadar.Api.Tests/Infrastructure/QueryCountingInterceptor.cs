using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class QueryCountingInterceptor : DbCommandInterceptor
{
    private int _listingsSelectCount;
    private int _listingPhotoSelectCount;

    public int ListingsSelectCount => Volatile.Read(ref _listingsSelectCount);

    public int ListingPhotoSelectCount => Volatile.Read(ref _listingPhotoSelectCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _listingsSelectCount, 0);
        Interlocked.Exchange(ref _listingPhotoSelectCount, 0);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        CountSelects(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CountSelects(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void CountSelects(DbCommand command)
    {
        if (command.CommandText.Contains("FROM \"listings\"", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _listingsSelectCount);
        }

        if (command.CommandText.Contains("FROM \"listing_photos\"", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _listingPhotoSelectCount);
        }
    }
}
