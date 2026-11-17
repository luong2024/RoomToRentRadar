namespace RoomToRentRadar.Api.Features.SavedSearches;

public sealed class AlertsOptions
{
    public const string SectionName = "Alerts";

    public bool Enabled { get; set; } = true;

    public int SweepIntervalMinutes { get; set; } = 5;

    public int LookbackMinutes { get; set; } = 30;

    public int DailyCap { get; set; } = 3;
}
