using RoomToRentRadar.Client.Adapters.Session;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class SessionEventsTests
{
    [Test]
    public void Raise_NoSubscribers_NoThrow()
    {
        var events = new SessionEvents();

        Assert.DoesNotThrow(() => events.RaiseSessionExpired());
    }

    [Test]
    public void Raise_MultipleSubscribers_AllInvokedEvenIfOneThrows()
    {
        var logger = new CapturingLogger<SessionEvents>();
        var events = new SessionEvents(logger);
        var invoked = new List<int>();
        events.SessionExpired += (_, _) => invoked.Add(1);
        events.SessionExpired += (_, _) => throw new InvalidOperationException("listener boom");
        events.SessionExpired += (_, _) => invoked.Add(3);

        Assert.DoesNotThrow(() => events.RaiseSessionExpired());

        Assert.That(invoked, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(logger.Messages, Has.Some.Contain("SessionExpired listener threw"));
    }
}