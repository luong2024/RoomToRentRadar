using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Result model for US10 distance and travel time calculation service.
    /// Fulfills AC1 (motorbike, bus, walk estimates), AC2 (ICTU destination), and AC3 (fallback indicator).
    /// </summary>
    public class TravelEstimateResult
    {
        public double DistanceKm { get; set; }
        public int MotorbikeMinutes { get; set; }
        public int BusMinutes { get; set; }
        public int WalkMinutes { get; set; }
        public bool IsFallback { get; set; } = true;
        public string DestinationName { get; set; } = Constants.IctuCampusName;
        public double DestinationLatitude { get; set; } = Constants.IctuLatitude;
        public double DestinationLongitude { get; set; } = Constants.IctuLongitude;
    }

    /// <summary>
    /// Contract for US10 distance and estimated travel time calculation service.
    /// Acceptance Criteria:
    ///   AC1: Detail screen shows distance and estimated travel time by motorbike, bus, and walk.
    ///   AC2: The estimate uses ICTU campus as destination.
    ///   AC3: If routing service is unavailable, straight-line distance is shown as fallback.
    /// </summary>
    public interface ITravelTimeService
    {
        TravelEstimateResult EstimateTravelTime(
            double sourceLat, double sourceLon,
            double? destLat = null, double? destLon = null,
            string? destinationName = null);

        Task<TravelEstimateResult> EstimateTravelTimeAsync(
            double sourceLat, double sourceLon,
            double? destLat = null, double? destLon = null,
            string? destinationName = null,
            CancellationToken cancellationToken = default);

        double CalculateStraightLineDistanceKm(double lat1, double lon1, double lat2, double lon2);

        void PopulateListingEstimates(RoomListing listing, double? destLat = null, double? destLon = null);
    }

    /// <summary>
    /// TASK OWNER: T10.1 (US10 — Build distance &amp; estimated travel time calculation service).
    /// Implementation of <see cref="ITravelTimeService"/> using Haversine formula and Thai Nguyen terrain speed profiles.
    /// </summary>
    public class TravelTimeService : ITravelTimeService
    {
        /// <summary>
        /// AC1 &amp; AC2 &amp; AC3: Calculates distance and estimated travel time with ICTU campus as default destination.
        /// </summary>
        public virtual TravelEstimateResult EstimateTravelTime(
            double sourceLat, double sourceLon,
            double? destLat = null, double? destLon = null,
            string? destinationName = null)
        {
            var targetLat = destLat ?? Constants.IctuLatitude;
            var targetLon = destLon ?? Constants.IctuLongitude;
            var targetName = destinationName ?? Constants.IctuCampusName;

            var straightLineKm = CalculateStraightLineDistanceKm(sourceLat, sourceLon, targetLat, targetLon);

            // Baseline fallback calculation
            var roadDistanceKm = Math.Round(straightLineKm * 1.25, 2);

            int walkMins = straightLineKm <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling((roadDistanceKm / 4.5) * 60.0));
            int motorbikeMins = straightLineKm <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling((roadDistanceKm / 28.0) * 60.0) + 1);
            int busMins = straightLineKm <= 0 ? 0 : (straightLineKm < 0.6 ? walkMins : Math.Max(8, (int)Math.Ceiling(8 + (roadDistanceKm / 18.0) * 60.0)));

            return new TravelEstimateResult
            {
                DistanceKm = straightLineKm,
                MotorbikeMinutes = motorbikeMins,
                BusMinutes = busMins,
                WalkMinutes = walkMins,
                IsFallback = true,
                DestinationName = targetName,
                DestinationLatitude = targetLat,
                DestinationLongitude = targetLon
            };
        }

        public virtual Task<TravelEstimateResult> EstimateTravelTimeAsync(
            double sourceLat, double sourceLon,
            double? destLat = null, double? destLon = null,
            string? destinationName = null,
            CancellationToken cancellationToken = default)
        {
            var result = EstimateTravelTime(sourceLat, sourceLon, destLat, destLon, destinationName);
            return Task.FromResult(result);
        }

        /// <summary>
        /// AC3: Calculates straight-line distance in kilometers between two coordinates using Haversine formula.
        /// </summary>
        public virtual double CalculateStraightLineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            if (Math.Abs(lat1 - lat2) < 0.000001 && Math.Abs(lon1 - lon2) < 0.000001)
            {
                return 0.0;
            }

            const double earthRadiusKm = 6371.0;
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(earthRadiusKm * c, 2);
        }

        /// <summary>
        /// AC1: Populates distance and travel time fields on a <see cref="RoomListing"/> entity.
        /// </summary>
        public virtual void PopulateListingEstimates(RoomListing listing, double? destLat = null, double? destLon = null)
        {
            if (listing is null) return;

            var estimate = EstimateTravelTime(listing.Latitude, listing.Longitude, destLat, destLon);
            listing.DistanceToCampusKm = estimate.DistanceKm;
            listing.MotorbikeMinutes = estimate.MotorbikeMinutes;
            listing.BusMinutes = estimate.BusMinutes;
            listing.WalkMinutes = estimate.WalkMinutes;
        }
    }
}
