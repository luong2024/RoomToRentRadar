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
        public string RouteSummary { get; set; } = string.Empty;
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

        void PopulateListingsEstimates(IEnumerable<RoomListing> listings, double? destLat = null, double? destLon = null);
    }

    /// <summary>
    /// TASK OWNER: T10.1 (US10 — Build distance &amp; estimated travel time calculation service).
    /// Implementation of <see cref="ITravelTimeService"/> using Haversine formula and Thai Nguyen terrain speed profiles.
    /// </summary>
    public class TravelTimeService : ITravelTimeService
    {
        // Thai Nguyen urban terrain benchmarks
        protected const double RoadWindingFactor = 1.25;       // Winding road distance multiplier
        protected const double WalkSpeedKmH = 4.5;              // Average walking speed (km/h)
        protected const double MotorbikeSpeedKmH = 28.0;        // Average motorbike urban speed (km/h)
        protected const double BusSpeedKmH = 18.0;              // Average bus speed in Thai Nguyen (km/h)
        protected const int MotorbikePrepMinutes = 1;           // Engine start & parking time
        protected const int BusWaitMinutes = 8;                 // Average bus waiting time at stop

        /// <summary>
        /// AC1 &amp; AC2 &amp; AC3: Calculates distance and estimated travel time with ICTU campus as default destination.
        /// When routing is unavailable, computes straight-line distance with realistic Thai Nguyen speed models as fallback.
        /// </summary>
        public virtual TravelEstimateResult EstimateTravelTime(
            double sourceLat, double sourceLon,
            double? destLat = null, double? destLon = null,
            string? destinationName = null)
        {
            // AC2: Default destination is ICTU Campus
            var targetLat = destLat ?? Constants.IctuLatitude;
            var targetLon = destLon ?? Constants.IctuLongitude;
            var targetName = string.IsNullOrWhiteSpace(destinationName) ? Constants.IctuCampusName : destinationName.Trim();

            // Sanitize coordinates
            sourceLat = Math.Clamp(sourceLat, -90.0, 90.0);
            sourceLon = Math.Clamp(sourceLon, -180.0, 180.0);
            targetLat = Math.Clamp(targetLat, -90.0, 90.0);
            targetLon = Math.Clamp(targetLon, -180.0, 180.0);

            // AC3: Calculate straight-line distance via Haversine formula
            var straightLineKm = CalculateStraightLineDistanceKm(sourceLat, sourceLon, targetLat, targetLon);

            // Edge case: Same location (distance is 0)
            if (straightLineKm <= 0.0)
            {
                return new TravelEstimateResult
                {
                    DistanceKm = 0.0,
                    MotorbikeMinutes = 0,
                    BusMinutes = 0,
                    WalkMinutes = 0,
                    IsFallback = true,
                    DestinationName = targetName,
                    DestinationLatitude = targetLat,
                    DestinationLongitude = targetLon,
                    RouteSummary = $"Tại điểm đến: {targetName}"
                };
            }

            // AC1 & AC3: Compute realistic road-equivalent distance and transport mode times
            var roadDistanceKm = Math.Round(straightLineKm * RoadWindingFactor, 2);

            // Walk: 4.5 km/h -> ~13.3 mins/km
            int walkMins = Math.Max(1, (int)Math.Ceiling((roadDistanceKm / WalkSpeedKmH) * 60.0));

            // Motorbike: 28 km/h + 1 min prep
            int motorbikeMins = Math.Max(1, (int)Math.Ceiling((roadDistanceKm / MotorbikeSpeedKmH) * 60.0) + MotorbikePrepMinutes);

            // Bus: for very short distances (< 600m), walking is faster than waiting for a bus
            int busMins;
            if (straightLineKm < 0.6)
            {
                busMins = walkMins;
            }
            else
            {
                busMins = Math.Max(BusWaitMinutes, (int)Math.Ceiling(BusWaitMinutes + (roadDistanceKm / BusSpeedKmH) * 60.0));
            }

            return new TravelEstimateResult
            {
                DistanceKm = straightLineKm,
                MotorbikeMinutes = motorbikeMins,
                BusMinutes = busMins,
                WalkMinutes = walkMins,
                IsFallback = true, // Straight-line fallback per AC3
                DestinationName = targetName,
                DestinationLatitude = targetLat,
                DestinationLongitude = targetLon,
                RouteSummary = $"Khoảng cách: {straightLineKm:F1} km tới {targetName} (Ước lượng dự phòng)"
            };
        }

        /// <summary>
        /// Asynchronous overload for estimated travel time calculation.
        /// </summary>
        public virtual Task<TravelEstimateResult> EstimateTravelTimeAsync(
            double sourceLat, double sourceLon,
            double? destLat = null, double? destLon = null,
            string? destinationName = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        /// <summary>
        /// AC1: Populates distance and travel time fields on a collection of <see cref="RoomListing"/> entities.
        /// </summary>
        public virtual void PopulateListingsEstimates(IEnumerable<RoomListing> listings, double? destLat = null, double? destLon = null)
        {
            if (listings is null) return;

            foreach (var listing in listings)
            {
                PopulateListingEstimates(listing, destLat, destLon);
            }
        }
    }
}
