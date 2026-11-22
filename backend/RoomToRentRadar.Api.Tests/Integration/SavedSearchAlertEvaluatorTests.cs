using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class SavedSearchAlertEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private ListingsApiFactory _factory = null!;
    private Guid _userId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory();
        await _factory.InitializeDatabaseAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _factory.ResetDataAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await TestUsersSeed.SeedAsync(dbContext);
        _userId = user.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task Sweep_NewPublishedListingInsideArea_DeliversInboxNotification()
    {
        var search = await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        var listingId = await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2));

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(1));
        var notifications = await NotificationsAsync();
        Assert.That(notifications, Has.Count.EqualTo(1));

        var notification = notifications[0];
        Assert.Multiple(() =>
        {
            Assert.That(notification.UserId, Is.EqualTo(_userId));
            Assert.That(notification.SavedSearchId, Is.EqualTo(search.Id));
            Assert.That(notification.ListingId, Is.EqualTo(listingId));
            Assert.That(notification.Title, Is.EqualTo("Phòng mới trong “Quận 10”"));
            Assert.That(notification.Body, Does.Contain("Phòng mới"));
            Assert.That(notification.Body, Does.Contain("3.500.000"));
            Assert.That(notification.Body, Does.Contain("25 m²"));
            Assert.That(notification.CreatedAt, Is.EqualTo(Now));
        });

        var logs = await LogsAsync(search.Id);
        Assert.Multiple(() =>
        {
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].ListingId, Is.EqualTo(listingId));
            Assert.That(logs[0].Delivered, Is.True);
            Assert.That(logs[0].CreatedAt, Is.EqualTo(Now));
        });
    }

    [Test]
    public async Task Sweep_RepeatedSweep_DoesNotDuplicateAlert()
    {
        await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2));

        var first = await SweepAsync(Now);
        var second = await SweepAsync(Now.AddMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(0));
        });
        Assert.That(await NotificationsAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Sweep_ListingPublishedBeforeActivation_IsIgnored()
    {
        await AddSearchAsync(activatedAt: Now.AddMinutes(-1));
        await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-10));

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);
    }

    [Test]
    public async Task Sweep_ListingOutsideLookbackWindow_IsIgnored()
    {
        await AddSearchAsync(activatedAt: Now.AddMinutes(-90));
        await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-45));

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);
    }

    [Test]
    public async Task Sweep_ListingOutsideDrawnArea_IsIgnored()
    {
        await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        await AddListingAsync(10.90, 106.90, updatedAt: Now.AddMinutes(-2));

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);
    }

    [Test]
    public async Task Sweep_NonPublishedListing_IsIgnored()
    {
        await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2), status: ListingStatus.Draft);

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);
    }

    [Test]
    public async Task Sweep_PausedSearch_ReceivesNothing()
    {
        await AddSearchAsync(SavedSearchAlertState.Paused, activatedAt: Now.AddMinutes(-10));
        await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2));

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);
    }

    [Test]
    public async Task Sweep_DeletedSearch_ReceivesNothing()
    {
        var search = await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await dbContext.SavedSearches.SingleAsync(candidate => candidate.Id == search.Id);
            dbContext.SavedSearches.Remove(stored);
            await dbContext.SaveChangesAsync();
        }

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);
    }

    [Test]
    public async Task Sweep_MoreThanThreeMatchesInADay_DeliversThreeAndSuppressesTheRest()
    {
        var search = await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        for (var index = 0; index < 5; index++)
        {
            await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2));
        }

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(3));
        Assert.That(await NotificationsAsync(), Has.Count.EqualTo(3));

        var logs = await LogsAsync(search.Id);
        Assert.Multiple(() =>
        {
            Assert.That(logs, Has.Count.EqualTo(5));
            Assert.That(logs.Count(log => log.Delivered), Is.EqualTo(3));
            Assert.That(logs.Count(log => !log.Delivered), Is.EqualTo(2));
        });

        var again = await SweepAsync(Now.AddMinutes(5));
        Assert.That(again, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Sweep_NewDayInVietnam_ResetsTheDailyCap()
    {
        // 18:00 UTC is 01:00 the next day in Asia/Ho_Chi_Minh, so the product
        // day started at 17:00 UTC.
        var utcNow = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc);
        var search = await AddSearchAsync(activatedAt: utcNow.AddMinutes(-60));

        for (var index = 0; index < 3; index++)
        {
            await AddAlertLogAsync(search.Id, utcNow.AddMinutes(-90), delivered: true);
        }

        for (var index = 0; index < 4; index++)
        {
            await AddListingAsync(10.70, 106.70, updatedAt: utcNow.AddMinutes(-5));
        }

        var delivered = await SweepAsync(utcNow);

        Assert.That(delivered, Is.EqualTo(3));
        Assert.That(await NotificationsAsync(), Has.Count.EqualTo(3));

        var logs = await LogsAsync(search.Id);
        Assert.Multiple(() =>
        {
            Assert.That(logs, Has.Count.EqualTo(7));
            Assert.That(logs.Count(log => log.Delivered), Is.EqualTo(6));
            Assert.That(logs.Count(log => !log.Delivered), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Sweep_ThreeAlertsAlreadyDeliveredToday_SuppressesNewMatches()
    {
        var search = await AddSearchAsync(activatedAt: Now.AddMinutes(-10));
        for (var index = 0; index < 3; index++)
        {
            await AddAlertLogAsync(search.Id, Now.AddMinutes(-60), delivered: true);
        }

        var listingId = await AddListingAsync(10.70, 106.70, updatedAt: Now.AddMinutes(-2));

        var delivered = await SweepAsync(Now);

        Assert.That(delivered, Is.EqualTo(0));
        Assert.That(await NotificationsAsync(), Is.Empty);

        var logs = await LogsAsync(search.Id);
        Assert.Multiple(() =>
        {
            Assert.That(logs, Has.Count.EqualTo(4));
            var suppressed = logs.Single(log => log.ListingId == listingId);
            Assert.That(suppressed.Delivered, Is.False);
        });
    }

    private static AreaPolygon DefaultArea() => new()
    {
        Type = "Polygon",
        Coordinates =
        [
            [
                [106.69, 10.69],
                [106.71, 10.69],
                [106.71, 10.71],
                [106.69, 10.71],
                [106.69, 10.69]
            ]
        ]
    };

    private async Task<SavedSearch> AddSearchAsync(
        SavedSearchAlertState alertState = SavedSearchAlertState.Active,
        DateTime? activatedAt = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var search = new SavedSearch
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Name = "Quận 10",
            AreaJson = JsonSerializer.Serialize(DefaultArea(), ApiJson.Options),
            AlertState = alertState,
            AlertsActivatedAt = activatedAt,
            CreatedAt = activatedAt ?? Now.AddHours(-1),
            UpdatedAt = activatedAt ?? Now.AddHours(-1)
        };
        dbContext.SavedSearches.Add(search);
        await dbContext.SaveChangesAsync();
        return search;
    }

    private async Task<Guid> AddListingAsync(
        double lat,
        double lng,
        DateTime updatedAt,
        ListingStatus status = ListingStatus.Published)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            Title = "Phòng mới",
            Description = "Mô tả",
            Price = 3_500_000,
            Deposit = 3_500_000,
            AreaM2 = 25,
            Bedrooms = 1,
            RoomType = RoomType.Studio,
            Address = "Đường 3/2, Quận 10",
            Lat = lat,
            Lng = lng,
            Status = status,
            CreatedAt = updatedAt.AddMinutes(-5),
            UpdatedAt = updatedAt
        };
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();
        return listing.Id;
    }

    private async Task AddAlertLogAsync(Guid savedSearchId, DateTime createdAt, bool delivered)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.SavedSearchAlertLogs.Add(new SavedSearchAlertLog
        {
            Id = Guid.NewGuid(),
            SavedSearchId = savedSearchId,
            ListingId = Guid.NewGuid(),
            CreatedAt = createdAt,
            Delivered = delivered
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<int> SweepAsync(DateTime utcNow)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<ISavedSearchAlertUseCase>();
        return await evaluator.SweepAsync(utcNow, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Notification>> NotificationsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Notifications
            .AsNoTracking()
            .OrderBy(notification => notification.CreatedAt)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<SavedSearchAlertLog>> LogsAsync(Guid savedSearchId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.SavedSearchAlertLogs
            .AsNoTracking()
            .Where(log => log.SavedSearchId == savedSearchId)
            .ToListAsync();
    }
}
