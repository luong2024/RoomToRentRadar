using System;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.Navigation;

[TestFixture]
public class AppSessionTests
{
    private ISessionStore _sessionStore = null!;
    private SessionEvents _sessionEvents = null!;

    [SetUp]
    public void SetUp()
    {
        _sessionStore = Substitute.For<ISessionStore>();
        _sessionEvents = new SessionEvents();
    }

    private static UserProfileDto Profile(string role) => new(
        Id: Guid.NewGuid().ToString(),
        Email: "user@ictu.edu.vn",
        FullName: "Test User",
        Role: role,
        AuthProvider: "EmailPassword",
        PhoneNumber: null,
        StudentCode: null,
        IsVerified: true,
        CreatedAtUtc: DateTime.UtcNow);

    [Test]
    public async Task AppSession_RefreshWithCachedLandlord_RaisesRoleChangedAndSetsRole()
    {
        _sessionStore.GetCachedUserProfileAsync().Returns(Profile("Landlord"));
        var session = new AppSession(_sessionStore, _sessionEvents);
        var raised = 0;
        session.RoleChanged += (_, _) => raised++;

        await session.RefreshAsync();

        Assert.That(session.CurrentRole, Is.EqualTo("Landlord"));
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public async Task AppSession_RefreshNoProfile_RoleIsNullGuest()
    {
        _sessionStore.GetCachedUserProfileAsync().Returns((UserProfileDto?)null);
        var session = new AppSession(_sessionStore, _sessionEvents);

        await session.RefreshAsync();

        Assert.That(session.CurrentRole, Is.Null);
    }

    [Test]
    public async Task AppSession_SessionExpired_ClearsRoleAndRaisesSessionEnded()
    {
        _sessionStore.GetCachedUserProfileAsync().Returns(Profile("Student"));
        var session = new AppSession(_sessionStore, _sessionEvents);
        await session.RefreshAsync();
        Assert.That(session.CurrentRole, Is.EqualTo("Student"));

        var ended = 0;
        session.SessionEnded += (_, _) => ended++;

        _sessionEvents.RaiseSessionExpired();

        Assert.That(session.CurrentRole, Is.Null);
        Assert.That(ended, Is.EqualTo(1));
    }

    [Test]
    public async Task AppSession_RefreshWhenStoreThrows_FallsBackToGuest()
    {
        _sessionStore.GetCachedUserProfileAsync()
            .Returns(Task.FromException<UserProfileDto?>(new InvalidOperationException("store down")));
        var session = new AppSession(_sessionStore, _sessionEvents);

        await session.RefreshAsync();

        Assert.That(session.CurrentRole, Is.Null);
    }
}