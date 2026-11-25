-- T09.1 / erd 4.15: cached campus travel times (30-day TTL, invalidated on pin move).
CREATE TABLE TravelTimeEstimates (
    RoomId TEXT NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    CampusId TEXT NOT NULL REFERENCES Campuses(Id),
    Mode TEXT NOT NULL,
    DistanceMeters INTEGER NOT NULL,
    DurationSeconds INTEGER NOT NULL,
    Source TEXT NOT NULL,
    ComputedAtUtc TEXT NOT NULL,
    PRIMARY KEY (RoomId, CampusId, Mode)
);

CREATE INDEX IX_TravelTimeEstimates_ComputedAtUtc ON TravelTimeEstimates(ComputedAtUtc);
