CREATE TABLE Users (
    Id TEXT PRIMARY KEY NOT NULL,
    Email TEXT NOT NULL,
    NormalizedEmail TEXT NOT NULL UNIQUE,
    FullName TEXT NOT NULL,
    PasswordHash TEXT NULL,
    Role TEXT NOT NULL,
    AuthProvider TEXT NOT NULL,
    SsoSubjectId TEXT NULL,
    PhoneNumber TEXT NULL,
    StudentCode TEXT NULL,
    IsVerified INTEGER NOT NULL,
    FailedLoginCount INTEGER NOT NULL,
    LockoutEndUtc TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE TABLE UserRefreshTokens (
    TokenHash TEXT PRIMARY KEY NOT NULL,
    UserId TEXT NOT NULL,
    JwtId TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    ExpiresAtUtc TEXT NOT NULL,
    RevokedAtUtc TEXT NULL,
    ReplacedByTokenHash TEXT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE AuditLogs (
    Id TEXT PRIMARY KEY NOT NULL,
    UserId TEXT NULL,
    UserEmail TEXT NULL,
    Role TEXT NOT NULL,
    Action TEXT NOT NULL,
    EntityType TEXT NOT NULL,
    EntityId TEXT NULL,
    DetailsJson TEXT NULL,
    IpAddress TEXT NULL,
    UserAgent TEXT NULL,
    StatusCode INTEGER NULL,
    DurationMs INTEGER NULL,
    IsSuccess INTEGER NOT NULL,
    ErrorMessage TEXT NULL,
    TimestampUtc TEXT NOT NULL
);

-- ERD §4.1: campus reference data (seeded in CampusSeedData, BR-CMP-01..03).
CREATE TABLE Campuses (
    Id TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    ShortName TEXT NOT NULL,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0
);

-- ERD §4.4: full Rooms table (33 columns). ConcurrencyToken is derived from
-- Version (ERD §1), never stored as a second source of truth.
CREATE TABLE Rooms (
    Id TEXT PRIMARY KEY NOT NULL,
    LandlordId TEXT NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT NULL,
    RoomType TEXT NOT NULL,
    MonthlyRentVnd INTEGER NOT NULL,
    DepositVnd INTEGER NOT NULL,
    AreaM2 REAL NOT NULL,
    MaxOccupants INTEGER NULL,
    Furnishing TEXT NULL,
    AvailableFrom TEXT NULL,
    AddressLine TEXT NOT NULL,
    Ward TEXT NOT NULL,
    District TEXT NOT NULL,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    ElectricityPriceVndPerKwh INTEGER NULL,
    WaterPriceVnd INTEGER NULL,
    WaterPriceUnit TEXT NULL,
    InternetFeeVnd INTEGER NULL,
    ParkingFeeVnd INTEGER NULL,
    OtherFeesNote TEXT NULL,
    Status TEXT NOT NULL,
    StatusReason TEXT NULL,
    NeedsReReview INTEGER NOT NULL DEFAULT 0,
    IsSafetyVerified INTEGER NOT NULL DEFAULT 0,
    VerifiedAtUtc TEXT NULL,
    VerifiedByUserId TEXT NULL,
    PublishedAtUtc TEXT NULL,
    LastUpdatedAtUtc TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    Version INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IX_Users_NormalizedEmail ON Users(NormalizedEmail);
CREATE INDEX IX_UserRefreshTokens_UserId ON UserRefreshTokens(UserId);
CREATE INDEX IX_AuditLogs_TimestampUtc ON AuditLogs(TimestampUtc DESC);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
CREATE INDEX IX_AuditLogs_Action ON AuditLogs(Action);

-- ERD §4.4 indexes (ADR-006 bounding-box prefilter + search/sort).
CREATE INDEX IX_Rooms_Status_Lat_Lng ON Rooms(Status, Latitude, Longitude);
CREATE INDEX IX_Rooms_Status_Rent ON Rooms(Status, MonthlyRentVnd);
CREATE INDEX IX_Rooms_LandlordId ON Rooms(LandlordId);
CREATE INDEX IX_Rooms_Status_PublishedAtUtc ON Rooms(Status, PublishedAtUtc);
CREATE INDEX IX_Rooms_NeedsReReview ON Rooms(NeedsReReview) WHERE NeedsReReview = 1;