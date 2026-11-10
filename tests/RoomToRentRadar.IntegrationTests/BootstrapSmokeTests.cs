namespace RoomToRentRadar.IntegrationTests;

public class BootstrapSmokeTests
{
    [Test]
    public void ProgramType_ShouldBeAccessibleForIntegrationTesting()
    {
        Assert.That(typeof(Program), Is.Not.Null);
    }
}
