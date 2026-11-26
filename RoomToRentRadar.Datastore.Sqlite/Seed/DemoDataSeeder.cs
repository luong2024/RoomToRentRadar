using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Datastore.Sqlite.Seed;

/// <summary>
/// T00.10: idempotently seeds the full 30-room demo catalog, its child tables
/// (<c>RoomAmenities</c>, <c>RoomSafetyFeatures</c>, <c>HouseRuleVersions</c>) and its photos
/// (<c>RoomPhotos</c>, only for rooms without any photo).
/// Child rows are inserted only when a room currently has zero child rows across all three tables
/// so subsequent startups never resurrect amenities/rules edited or removed by a user.
/// </summary>
internal static class DemoDataSeeder
{
    public static void Seed(SqliteConnection conn, IReadOnlyList<SeedPhoto>? photos = null)
    {
        ArgumentNullException.ThrowIfNull(conn);

        var entries = DemoRoomCatalog.CreateAllEntries();
        using var tx = conn.BeginTransaction();

        foreach (var entry in entries)
        {
            DemoRoomRowWriter.InsertOrIgnore(conn, tx, entry.Room);

            if (!HasAnyChildRows(conn, tx, entry.Room.Id))
            {
                InsertAmenities(conn, tx, entry.Room);
                InsertSafetyFeatures(conn, tx, entry.Room);
                InsertHouseRuleVersions(conn, tx, entry.HouseRuleVersions);
            }
        }

        if (photos is { Count: > 0 })
        {
            InsertPhotos(conn, tx, entries, photos);
        }

        tx.Commit();
    }

    // BR-MED-01: three photos per room — two of the room's own type (the first is the cover, BR-MED-05)
    // and one supplementary shot. The choice depends only on catalog order, so it is stable across runs.
    private static void InsertPhotos(
        SqliteConnection conn,
        SqliteTransaction tx,
        IReadOnlyList<DemoRoomCatalog.SeededRoomEntry> entries,
        IReadOnlyList<SeedPhoto> photos)
    {
        var supplementary = photos.Where(p => p.RoomType is null).ToList();
        var seenPerType = new Dictionary<RoomType, int>();

        for (var index = 0; index < entries.Count; index++)
        {
            var room = entries[index].Room;
            var typed = photos.Where(p => p.RoomType == room.RoomType).ToList();
            var ordinal = seenPerType.GetValueOrDefault(room.RoomType);
            seenPerType[room.RoomType] = ordinal + 1;

            if (typed.Count == 0 || HasPhotos(conn, tx, room.Id))
            {
                continue;
            }

            var selection = new List<SeedPhoto> { typed[ordinal % typed.Count] };
            if (typed.Count > 1)
            {
                selection.Add(typed[(ordinal + 1) % typed.Count]);
            }

            if (supplementary.Count > 0)
            {
                selection.Add(supplementary[index % supplementary.Count]);
            }

            for (var sortOrder = 0; sortOrder < selection.Count; sortOrder++)
            {
                InsertPhoto(conn, tx, room, selection[sortOrder], sortOrder);
            }
        }
    }

    private static bool HasPhotos(SqliteConnection conn, SqliteTransaction tx, string roomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM RoomPhotos WHERE RoomId = @roomId);";
        cmd.Parameters.AddWithValue("@roomId", roomId);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    private static void InsertPhoto(
        SqliteConnection conn,
        SqliteTransaction tx,
        Room room,
        SeedPhoto photo,
        int sortOrder)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        // AttachedAtUtc is set so the unattached-upload sweep (BR-MED-03) never removes these rows.
        cmd.CommandText = """
            INSERT OR IGNORE INTO RoomPhotos (
                Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
                OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
            ) VALUES (
                @id, @roomId, @userId, @kind, @contentType, @sizeBytes, @width, @height,
                @mediumPath, @mediumPath, @thumbPath, @sortOrder, @createdAtUtc, @createdAtUtc
            );
            """;
        cmd.Parameters.AddWithValue("@id", $"photo-{room.Id}-{sortOrder + 1}");
        cmd.Parameters.AddWithValue("@roomId", room.Id);
        cmd.Parameters.AddWithValue("@userId", room.LandlordId);
        cmd.Parameters.AddWithValue("@kind", PhotoKind.Standard.ToString());
        cmd.Parameters.AddWithValue("@contentType", RoomPhoto.JpegContentType);
        cmd.Parameters.AddWithValue("@sizeBytes", photo.SizeBytes);
        cmd.Parameters.AddWithValue("@width", photo.Width);
        cmd.Parameters.AddWithValue("@height", photo.Height);
        cmd.Parameters.AddWithValue("@mediumPath", photo.MediumPath);
        cmd.Parameters.AddWithValue("@thumbPath", photo.ThumbPath);
        cmd.Parameters.AddWithValue("@sortOrder", sortOrder);
        cmd.Parameters.AddWithValue("@createdAtUtc", room.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private static bool HasAnyChildRows(SqliteConnection conn, SqliteTransaction tx, string roomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(1) FROM RoomAmenities WHERE RoomId = @roomId) +
                (SELECT COUNT(1) FROM RoomSafetyFeatures WHERE RoomId = @roomId) +
                (SELECT COUNT(1) FROM HouseRuleVersions WHERE RoomId = @roomId);
            """;
        cmd.Parameters.AddWithValue("@roomId", roomId);

        var count = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return count > 0;
    }

    private static void InsertAmenities(SqliteConnection conn, SqliteTransaction tx, Room room)
    {
        if (room.Amenities.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO RoomAmenities (RoomId, AmenityCode) VALUES (@roomId, @amenityCode);";
        cmd.Parameters.AddWithValue("@roomId", room.Id);
        var codeParam = cmd.Parameters.Add("@amenityCode", SqliteType.Text);

        foreach (var amenity in room.Amenities)
        {
            codeParam.Value = amenity.ToString();
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertSafetyFeatures(SqliteConnection conn, SqliteTransaction tx, Room room)
    {
        if (room.SafetyFeatures.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO RoomSafetyFeatures (RoomId, FeatureCode, IsConfirmed)
            VALUES (@roomId, @featureCode, @isConfirmed);
            """;
        cmd.Parameters.AddWithValue("@roomId", room.Id);
        var codeParam = cmd.Parameters.Add("@featureCode", SqliteType.Text);
        var confirmedParam = cmd.Parameters.Add("@isConfirmed", SqliteType.Integer);

        foreach (var entry in room.SafetyFeatures)
        {
            codeParam.Value = entry.Feature.ToString();
            confirmedParam.Value = entry.IsConfirmed ? 1 : 0;
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertHouseRuleVersions(
        SqliteConnection conn,
        SqliteTransaction tx,
        IReadOnlyList<HouseRuleVersion> versions)
    {
        if (versions.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO HouseRuleVersions (
                Id, RoomId, VersionNumber, QuietHoursStart, QuietHoursEnd, CurfewTime, GuestPolicy,
                GuestPolicyNote, CookingPolicy, ParkingAvailable, ParkingRules, PetsAllowed,
                SmokingAllowed, OtherRules, CreatedByUserId, CreatedAtUtc
            ) VALUES (
                @id, @roomId, @versionNumber, @quietHoursStart, @quietHoursEnd, @curfewTime, @guestPolicy,
                @guestPolicyNote, @cookingPolicy, @parkingAvailable, @parkingRules, @petsAllowed,
                @smokingAllowed, @otherRules, @createdByUserId, @createdAtUtc
            );
            """;

        var idParam = cmd.Parameters.Add("@id", SqliteType.Text);
        var roomIdParam = cmd.Parameters.Add("@roomId", SqliteType.Text);
        var versionParam = cmd.Parameters.Add("@versionNumber", SqliteType.Integer);
        var quietStartParam = cmd.Parameters.Add("@quietHoursStart", SqliteType.Text);
        var quietEndParam = cmd.Parameters.Add("@quietHoursEnd", SqliteType.Text);
        var curfewParam = cmd.Parameters.Add("@curfewTime", SqliteType.Text);
        var guestPolicyParam = cmd.Parameters.Add("@guestPolicy", SqliteType.Text);
        var guestNoteParam = cmd.Parameters.Add("@guestPolicyNote", SqliteType.Text);
        var cookingPolicyParam = cmd.Parameters.Add("@cookingPolicy", SqliteType.Text);
        var parkingAvailableParam = cmd.Parameters.Add("@parkingAvailable", SqliteType.Integer);
        var parkingRulesParam = cmd.Parameters.Add("@parkingRules", SqliteType.Text);
        var petsParam = cmd.Parameters.Add("@petsAllowed", SqliteType.Integer);
        var smokingParam = cmd.Parameters.Add("@smokingAllowed", SqliteType.Integer);
        var otherRulesParam = cmd.Parameters.Add("@otherRules", SqliteType.Text);
        var createdByParam = cmd.Parameters.Add("@createdByUserId", SqliteType.Text);
        var createdAtParam = cmd.Parameters.Add("@createdAtUtc", SqliteType.Text);

        foreach (var rules in versions)
        {
            idParam.Value = rules.Id;
            roomIdParam.Value = rules.RoomId;
            versionParam.Value = rules.VersionNumber;
            quietStartParam.Value = (object?)rules.QuietHoursStart ?? DBNull.Value;
            quietEndParam.Value = (object?)rules.QuietHoursEnd ?? DBNull.Value;
            curfewParam.Value = (object?)rules.CurfewTime ?? DBNull.Value;
            guestPolicyParam.Value = rules.GuestPolicy.ToString();
            guestNoteParam.Value = (object?)rules.GuestPolicyNote ?? DBNull.Value;
            cookingPolicyParam.Value = rules.CookingPolicy.ToString();
            parkingAvailableParam.Value = rules.ParkingAvailable ? 1 : 0;
            parkingRulesParam.Value = (object?)rules.ParkingRules ?? DBNull.Value;
            petsParam.Value = rules.PetsAllowed ? 1 : 0;
            smokingParam.Value = rules.SmokingAllowed ? 1 : 0;
            otherRulesParam.Value = (object?)rules.OtherRules ?? DBNull.Value;
            createdByParam.Value = rules.CreatedByUserId;
            createdAtParam.Value = rules.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
            cmd.ExecuteNonQuery();
        }
    }
}
