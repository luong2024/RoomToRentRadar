namespace RoomToRentRadar.Client.Adapters.Mocks;

/// <summary>
/// Mocks are a Debug-only development aid (PLAN Q8): <c>ApiOptions.UseMocks</c> must never take
/// effect in a Release build. Called by AddClientAdapters with the build flag so the rule is
/// testable in both configurations (CI runs tests in Release).
/// </summary>
public static class MockGuard
{
    public static void EnsureAllowed(bool useMocks, bool isDebugBuild)
    {
        if (useMocks && !isDebugBuild)
        {
            throw new InvalidOperationException("UseMocks is only allowed in Debug builds.");
        }
    }
}