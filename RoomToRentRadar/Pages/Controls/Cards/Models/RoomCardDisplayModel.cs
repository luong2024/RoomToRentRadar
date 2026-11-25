using System.Globalization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.Cards.Models;

/// <summary>
/// What a room card shows (SCR-HOME-01, US01 AC2). The text properties are formatted for the current
/// language when read, so a list must be rebuilt after a language switch (US15).
/// </summary>
public sealed record RoomCardDisplayModel
{
    private const decimal OneMillion = 1_000_000;

    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal PricePerMonth { get; init; }
    public double AreaSquareMeters { get; init; }
    public string Address { get; init; } = string.Empty;
    public double DistanceMeters { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public bool IsVerified { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
    public bool IsBookmarked { get; init; }
    public IReadOnlyList<string> Amenities { get; init; } = [];
    public RoomType? RoomType { get; init; }
    public Furnishing? Furnishing { get; init; }

    /// <summary>Short name of the campus <see cref="DistanceMeters"/> is measured from (BR-SRCH-03).</summary>
    public string CampusName { get; init; } = "ICTU";

    public string DistanceFormatted => DistanceMeters < 1000
        ? $"{DistanceMeters:F0}m from campus"
        : $"{(DistanceMeters / 1000.0):F1}km from campus";

    public string AreaFormatted => $"{AreaSquareMeters:F0} m²";

    /// <summary>"2.5M/mo" / "2,5 tr/tháng"; amounts under one million use the thousand unit.</summary>
    public string PriceText => PricePerMonth >= OneMillion
        ? Format("Card_PriceMillion", (PricePerMonth / OneMillion).ToString("0.###", Culture))
        : Format("Card_PriceThousand", (PricePerMonth / 1000).ToString("0.#", Culture));

    /// <summary>"25 m² · Boarding room · Furnished"; unknown parts are left out.</summary>
    public string MetaText
    {
        get
        {
            var parts = new List<string> { $"{AreaSquareMeters.ToString("0.#", Culture)} m²" };
            if (RoomType is { } roomType)
            {
                parts.Add(AppResources.GetString($"RoomType_{roomType}"));
            }

            if (Furnishing is { } furnishing)
            {
                parts.Add(AppResources.GetString($"Card_Furnishing_{furnishing}"));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>"1.2 km to ICTU"; every distance names its campus (docs/ui-ux-spec.md §2.3).</summary>
    public string DistanceText => string.Format(
        Culture,
        AppResources.GetString("Card_Distance"),
        (DistanceMeters / 1000.0).ToString("0.0", Culture),
        CampusName);

    /// <summary>Cover photo address; <c>null</c> shows the placeholder only (BR-MED-05).</summary>
    public Uri? ThumbnailUri =>
        Uri.TryCreate(ThumbnailUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;

    public string VerifiedText => AppResources.GetString("Card_Verified");

    public string CompareText => AppResources.GetString("Card_Compare");

    public string SaveDescription => Format("Card_SaveDescription", Title);

    /// <summary>One screen-reader sentence for the whole card.</summary>
    public string AccessibilityText => string.Join(
        ", ",
        new[] { Title, PriceText, MetaText, DistanceText, IsVerified ? VerifiedText : null }
            .Where(part => !string.IsNullOrEmpty(part)));

    private static CultureInfo Culture => AppResources.Culture ?? CultureInfo.CurrentCulture;

    private static string Format(string key, string value) =>
        string.Format(Culture, AppResources.GetString(key), value);
}
