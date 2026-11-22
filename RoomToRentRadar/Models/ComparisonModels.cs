namespace RoomToRentRadar.Models;

public sealed record ComparisonCandidate(
    Guid Id,
    string Title,
    string? CoverPhotoUrl,
    long Price,
    double AreaM2);

public sealed record ComparisonColumnHeader(
    Guid Id,
    string Title,
    string PriceText,
    string? PhotoSource,
    bool HasPhoto);

public sealed record ComparisonCell(string Text, bool IsBest);

public sealed record ComparisonRow(string Label, IReadOnlyList<ComparisonCell> Cells);
