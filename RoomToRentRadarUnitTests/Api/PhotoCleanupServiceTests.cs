using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar.Api.BackgroundJobs;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// BR-MED-03 / system-architecture §6.4: the hourly cleanup sweep must not run at startup, must honor
/// <c>Media:PhotoCleanupEnabled</c> and must resolve its use case only inside a (lazy) scope so the API
/// host - including every WebApplicationFactory test - boots without the datastore being swept.
/// </summary>
[TestFixture]
public class PhotoCleanupServiceTests
{
    [Test]
    public void CleanupInterval_IsOneHour()
    {
        Assert.That(PhotoCleanupService.CleanupInterval, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public async Task StartAsync_WhenDisabled_ReturnsWithoutCreatingAScope()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var configuration = BuildConfiguration(("Media:PhotoCleanupEnabled", "false"));
        var service = new PhotoCleanupService(
            scopeFactory,
            configuration,
            NullLogger<PhotoCleanupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Test]
    public async Task StartAsync_WhenEnabled_DoesNotSweepAtStartup()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var configuration = BuildConfiguration(("Media:PhotoCleanupEnabled", "true"));
        var service = new PhotoCleanupService(
            scopeFactory,
            configuration,
            NullLogger<PhotoCleanupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Test]
    public void Constructor_NullDependencies_ThrowArgumentNullException()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new PhotoCleanupService(
                null!,
                BuildConfiguration(),
                NullLogger<PhotoCleanupService>.Instance));
            Assert.Throws<ArgumentNullException>(() => new PhotoCleanupService(
                Substitute.For<IServiceScopeFactory>(),
                null!,
                NullLogger<PhotoCleanupService>.Instance));
            Assert.Throws<ArgumentNullException>(() => new PhotoCleanupService(
                Substitute.For<IServiceScopeFactory>(),
                BuildConfiguration(),
                null!));
        });
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();
}
