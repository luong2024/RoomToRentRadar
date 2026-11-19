using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Comparison.Services;

public sealed record RoomSecurityScoreResult(int Score, string Band);

public static class RoomSecurityScore
{
    private const int BaseScore = 40;

    /// <summary>
    /// Rental listings carry no security rating, so this is a deterministic
    /// estimate from safety-related amenities, surfaced in the UI as "ước tính".
    /// </summary>
    public static RoomSecurityScoreResult Calculate(IReadOnlyCollection<AmenityCode>? amenities)
    {
        var score = BaseScore;
        if (amenities is not null)
        {
            var set = amenities as ISet<AmenityCode> ?? new HashSet<AmenityCode>(amenities);
            if (set.Contains(AmenityCode.Security)) score += 25;
            if (set.Contains(AmenityCode.Parking)) score += 12;
            if (set.Contains(AmenityCode.Elevator)) score += 10;
            if (set.Contains(AmenityCode.PrivateBathroom)) score += 8;
            if (set.Contains(AmenityCode.Balcony)) score += 5;
        }

        score = Math.Min(score, 100);
        return new RoomSecurityScoreResult(score, DescribeBand(score));
    }

    private static string DescribeBand(int score) => score switch
    {
        >= 80 => "Rất an toàn",
        >= 60 => "An toàn",
        >= 45 => "Khá",
        _ => "Cần cân nhắc"
    };
}
