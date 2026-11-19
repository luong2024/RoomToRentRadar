using RoomToRentRadar.Client.Adapters.Mocks;

namespace RoomToRentRadarUnitTests.ClientAdapters.Mocks;

/// <summary>T00.14 Phase 8 (AC-4): UseMocks is a Debug-only aid; the guard is build-flag parameterised so it is testable in Release (CI).</summary>
[TestFixture]
public class MockGuardTests
{
    [Test]
    public void EnsureAllowed_UseMocksTrue_ReleaseBuild_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MockGuard.EnsureAllowed(useMocks: true, isDebugBuild: false));

        Assert.That(ex!.Message, Is.EqualTo("UseMocks is only allowed in Debug builds."));
    }

    [Test]
    public void EnsureAllowed_UseMocksTrue_DebugBuild_Ok()
    {
        Assert.DoesNotThrow(() => MockGuard.EnsureAllowed(useMocks: true, isDebugBuild: true));
    }

    [Test]
    public void EnsureAllowed_UseMocksFalse_AlwaysAllowed()
    {
        Assert.DoesNotThrow(() => MockGuard.EnsureAllowed(useMocks: false, isDebugBuild: true));
        Assert.DoesNotThrow(() => MockGuard.EnsureAllowed(useMocks: false, isDebugBuild: false));
    }
}