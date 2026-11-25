using System.Threading.Tasks;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;
using RoomToRentRadar.Utilities;

namespace RoomToRentRadarUnitTests;

[NUnit.Framework.TestFixture]
public class CommuteViewModelTests
{
    [NUnit.Framework.Test]
    public void HaversineDistanceHelper_IdenticalPoints_ReturnsZero()
    {
        double km = HaversineDistanceHelper.CalculateStraightLineKm(21.5850, 105.8066, 21.5850, 105.8066);

        NUnit.Framework.Assert.That(km, NUnit.Framework.Is.EqualTo(0.0).Within(0.0001));
    }

    [NUnit.Framework.Test]
    public void HaversineDistanceHelper_KnownIctuCoordinates_ReturnsExpectedShortDistance()
    {
        double km = HaversineDistanceHelper.CalculateStraightLineKm(21.5822, 105.8091, 21.5850, 105.8066);

        NUnit.Framework.Assert.That(km, NUnit.Framework.Is.GreaterThan(0.2).And.LessThan(1.0));
    }

    [NUnit.Framework.Test]
    public async Task LoadCommuteAsync_DefaultCampus_PopulatesMotorbikeBusWalkAndHidesFallback()
    {
        var vm = new CommuteViewModel(new MockCommuteService());

        await vm.LoadCommuteAsync();

        NUnit.Framework.Assert.That(vm.IsFallbackStraightLine, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.ShowTransportTimes, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.MotorbikeMinutes, NUnit.Framework.Is.GreaterThan(0));
        NUnit.Framework.Assert.That(vm.BusMinutes, NUnit.Framework.Is.GreaterThanOrEqualTo(vm.MotorbikeMinutes));
        NUnit.Framework.Assert.That(vm.WalkMinutes, NUnit.Framework.Is.GreaterThanOrEqualTo(vm.MotorbikeMinutes));
        NUnit.Framework.Assert.That(vm.MotorbikeText, NUnit.Framework.Does.Contain("Xe máy:"));
        NUnit.Framework.Assert.That(vm.BusText, NUnit.Framework.Does.Contain("Xe buýt:"));
        NUnit.Framework.Assert.That(vm.WalkText, NUnit.Framework.Does.Contain("Đi bộ:"));
    }

    [NUnit.Framework.Test]
    public async Task LoadForListingAsync_FallbackRoom_DisplaysStraightLineDistanceLabel()
    {
        var vm = new CommuteViewModel(new MockCommuteService());

        await vm.LoadForListingAsync("room-fallback");

        NUnit.Framework.Assert.That(vm.IsFallbackStraightLine, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.ShowTransportTimes, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.StraightLineKm, NUnit.Framework.Is.GreaterThan(0));
        NUnit.Framework.Assert.That(vm.FallbackDistanceText, NUnit.Framework.Does.StartWith("Khoảng cách đường chim bay: "));
        NUnit.Framework.Assert.That(vm.FallbackDistanceText, NUnit.Framework.Does.EndWith(" km"));
    }

    [NUnit.Framework.Test]
    public async Task ToggleSimulateFallbackCommand_SwitchesBetweenRoutingAndFallbackModes()
    {
        var vm = new CommuteViewModel(new MockCommuteService());
        await vm.LoadCommuteAsync();
        NUnit.Framework.Assert.That(vm.IsFallbackStraightLine, NUnit.Framework.Is.False);

        await vm.ToggleSimulateFallbackCommand.ExecuteAsync(null);
        NUnit.Framework.Assert.That(vm.SimulateFallback, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.IsFallbackStraightLine, NUnit.Framework.Is.True);
        NUnit.Framework.Assert.That(vm.FallbackDistanceText, NUnit.Framework.Does.StartWith("Khoảng cách đường chim bay: "));

        await vm.ToggleSimulateFallbackCommand.ExecuteAsync(null);
        NUnit.Framework.Assert.That(vm.SimulateFallback, NUnit.Framework.Is.False);
        NUnit.Framework.Assert.That(vm.IsFallbackStraightLine, NUnit.Framework.Is.False);
    }
}
