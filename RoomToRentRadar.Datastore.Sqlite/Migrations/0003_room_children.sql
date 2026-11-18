-- T00.9 / erd 4.5..4.7: BR-LST-08, BR-HR-01..02, GUARDRAILS 1.5.2
CREATE TABLE RoomAmenities (
    RoomId TEXT NOT NULL,
    AmenityCode TEXT NOT NULL,
    PRIMARY KEY (RoomId, AmenityCode),
    FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE
);

CREATE TABLE RoomSafetyFeatures (
    RoomId TEXT NOT NULL,
    FeatureCode TEXT NOT NULL,
    IsConfirmed INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (RoomId, FeatureCode),
    FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE
);

CREATE TABLE HouseRuleVersions (
    Id TEXT PRIMARY KEY NOT NULL,
    RoomId TEXT NOT NULL,
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
    CreatedByUserId TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    UNIQUE (RoomId, VersionNumber)
);

CREATE INDEX IX_HouseRuleVersions_RoomId ON HouseRuleVersions(RoomId);
