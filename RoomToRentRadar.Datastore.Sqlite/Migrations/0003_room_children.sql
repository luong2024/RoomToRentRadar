-- T00.9 / erd 4.5-4.7: amenity and safety-feature codes plus versioned house rules.
CREATE TABLE RoomAmenities (
    RoomId TEXT NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    AmenityCode TEXT NOT NULL,
    PRIMARY KEY (RoomId, AmenityCode)
);

-- ERD 4.6: rows are the features DECLARED by the landlord; Admin confirms them at approval.
CREATE TABLE RoomSafetyFeatures (
    RoomId TEXT NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    FeatureCode TEXT NOT NULL,
    IsConfirmed INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (RoomId, FeatureCode)
);

-- ERD 4.7: immutable rows, current rules = max VersionNumber per room (BR-HR-02).
CREATE TABLE HouseRuleVersions (
    Id TEXT PRIMARY KEY NOT NULL,
    RoomId TEXT NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    VersionNumber INTEGER NOT NULL,
    QuietHoursStart TEXT NULL,
    QuietHoursEnd TEXT NULL,
    CurfewTime TEXT NULL,
    GuestPolicy TEXT NOT NULL,
    GuestPolicyNote TEXT NULL,
    CookingPolicy TEXT NOT NULL,
    ParkingAvailable INTEGER NOT NULL,
    ParkingRules TEXT NULL,
    PetsAllowed INTEGER NOT NULL,
    SmokingAllowed INTEGER NOT NULL,
    OtherRules TEXT NULL,
    CreatedByUserId TEXT NOT NULL REFERENCES Users(Id),
    CreatedAtUtc TEXT NOT NULL,
    UNIQUE (RoomId, VersionNumber)
);
