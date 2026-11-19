-- T03.1 / erd 4.8: uploaded listing photos. RoomId stays NULL until the create/update
-- listing flow attaches the photo (BR-MED-03); older unattached rows are cleaned up after 24 h.
CREATE TABLE RoomPhotos (
    Id TEXT PRIMARY KEY NOT NULL,
    RoomId TEXT NULL REFERENCES Rooms(Id),
    UploadedByUserId TEXT NOT NULL REFERENCES Users(Id),
    Kind TEXT NOT NULL,
    ContentType TEXT NOT NULL,
    SizeBytes INTEGER NOT NULL,
    Width INTEGER NOT NULL,
    Height INTEGER NOT NULL,
    OriginalPath TEXT NOT NULL,
    MediumPath TEXT NOT NULL,
    ThumbPath TEXT NOT NULL,
    SortOrder INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    AttachedAtUtc TEXT NULL
);

CREATE INDEX IX_RoomPhotos_RoomId_SortOrder ON RoomPhotos(RoomId, SortOrder);
CREATE INDEX IX_RoomPhotos_UploadedByUserId_AttachedAtUtc ON RoomPhotos(UploadedByUserId, AttachedAtUtc);
