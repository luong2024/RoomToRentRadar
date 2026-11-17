namespace RoomToRentRadar.Client.Adapters;

/// <summary>Build configuration flag used by the mock guard; a const so Release builds trim the Debug path.</summary>
public static class ClientBuildInfo
{
#if DEBUG
    public const bool IsDebugBuild = true;
#else
    public const bool IsDebugBuild = false;
#endif
}