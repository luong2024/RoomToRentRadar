namespace RoomToRentRadar.Infrastructure.Persistence
{
    /// <summary>
    /// SQLite DDL for all Sprint 1 tables (OWNER: @architect).
    /// Changing a table here is a CONTRACT change: open a dedicated PR reviewed by the Scrum Master / Lead.
    /// All statements are idempotent (CREATE TABLE IF NOT EXISTS).
    /// </summary>
    public static class DatabaseSchema
    {
        public static readonly string[] TableNames =
        {
            "UserAccount", "RoomListing", "FavoriteRoom", "SavedSearchAlert", "HouseRuleVersion", "ListingReport"
        };

        public const string CreateTablesSql = @"
        CREATE TABLE IF NOT EXISTS UserAccount (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FullName TEXT NOT NULL,
            Email TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL,
            Phone TEXT NOT NULL,
            Role TEXT NOT NULL,
            SelectedCampus TEXT NOT NULL DEFAULT 'ICTU',
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS RoomListing (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Description TEXT NOT NULL,
            MonthlyRent REAL NOT NULL,
            Deposit REAL NOT NULL,
            Area REAL NOT NULL,
            RoomType TEXT NOT NULL,
            Address TEXT NOT NULL,
            Ward TEXT NOT NULL,
            Latitude REAL NOT NULL,
            Longitude REAL NOT NULL,
            DistanceToCampusKm REAL NOT NULL,
            MotorbikeMinutes INTEGER NOT NULL,
            BusMinutes INTEGER NOT NULL,
            WalkMinutes INTEGER NOT NULL,
            AmenitiesCsv TEXT NOT NULL,
            ThumbnailUrl TEXT NOT NULL,
            PhotosJson TEXT NOT NULL,
            Panorama360Url TEXT NOT NULL,
            LandlordId INTEGER NOT NULL,
            LandlordName TEXT NOT NULL,
            LandlordPhone TEXT NOT NULL,
            Status TEXT NOT NULL,
            IsVerified INTEGER NOT NULL,
            SecurityScore REAL NOT NULL,
            QuietHours TEXT NOT NULL,
            GuestPolicy TEXT NOT NULL,
            CookingRules TEXT NOT NULL,
            ParkingRules TEXT NOT NULL,
            RulesVersion INTEGER NOT NULL DEFAULT 1,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS FavoriteRoom (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            RoomId INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS SavedSearchAlert (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Name TEXT NOT NULL,
            MinPrice REAL NULL,
            MaxPrice REAL NULL,
            MinArea REAL NULL,
            MaxDistanceKm REAL NULL,
            RoomType TEXT NULL,
            CenterLatitude REAL NULL,
            CenterLongitude REAL NULL,
            RadiusKm REAL NULL,
            PolygonJson TEXT NOT NULL DEFAULT '[]',
            IsEnabled INTEGER NOT NULL DEFAULT 1,
            AlertsSentToday INTEGER NOT NULL DEFAULT 0,
            MaxDailyAlerts INTEGER NOT NULL DEFAULT 3,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS HouseRuleVersion (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RoomId INTEGER NOT NULL,
            VersionNumber INTEGER NOT NULL,
            QuietHours TEXT NOT NULL,
            GuestPolicy TEXT NOT NULL,
            CookingRules TEXT NOT NULL,
            ParkingRules TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ListingReport (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RoomId INTEGER NOT NULL,
            ReporterUserId INTEGER NOT NULL,
            Reason TEXT NOT NULL,
            Note TEXT NOT NULL,
            Status TEXT NOT NULL DEFAULT 'Pending',
            CreatedAt TEXT NOT NULL
        );";
    }
}