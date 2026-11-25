using RoomToRentRadar.Models;

namespace RoomToRentRadar.Abstractions.Reports
{
    /// <summary>
    /// Contract for US13 Listing Reports &amp; Moderation Queue. Implemented in T13.1. Consumed in T13.2.
    /// </summary>
    public interface IListingReportRepository
    {
        Task<int> SubmitReportAsync(ListingReport report);
        Task<List<ListingReport>> ListPendingReportsAsync();
    }
}
