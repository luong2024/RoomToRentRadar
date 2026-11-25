namespace RoomToRentRadar
{
    /// <summary>
    /// Shared application constants (OWNER: @architect — read-only for feature developers).
    /// Pure .NET: platform-specific values (e.g. database file path) are provided by the MAUI app
    /// through <c>DatabaseOptions</c>, never from here.
    /// </summary>
    public static class Constants
    {
        public const string DatabaseFilename = "AppSQLite.db3";

        // ICTU Campus Benchmark Coordinates (Quyet Thang, Thai Nguyen)
        public const string IctuCampusName = "Đại học CNTT & TT Thái Nguyên (ICTU)";
        public const double IctuLatitude = 21.5850;
        public const double IctuLongitude = 105.8064;

        /// <summary>
        /// Standardized Shell Navigation Routes for Sprint 1 (US01 -> US13).
        /// All team members must use these constants when navigating between screens.
        /// </summary>
        public static class Routes
        {
            public const string Home = "home";
            public const string Login = "login";
            public const string Register = "register";
            public const string RoomList = "room-list";
            public const string RoomDetail = "room-detail";
            public const string RoomGallery360 = "room-gallery-360";
            public const string RoomFilter = "room-filter";
            public const string RoomMap = "room-map";
            public const string Favorites = "favorites";
            public const string SearchAlerts = "search-alerts";
            public const string PublishListing = "publish-listing";
            public const string RoomCompare = "room-compare";
            public const string HouseRules = "house-rules";
            public const string ReportListing = "report-listing";
        }

        public static class UserRoles
        {
            public const string Student = "Student";
            public const string Landlord = "Landlord";
            public const string Admin = "Admin";
        }

        public static class ListingStatuses
        {
            public const string Available = "Available";
            public const string PendingReview = "PendingReview";
            public const string Unavailable = "Unavailable";
        }

        public static class ReportStatuses
        {
            public const string Pending = "Pending";
            public const string Reviewed = "Reviewed";
            public const string Resolved = "Resolved";
        }
    }
}
