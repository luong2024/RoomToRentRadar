using FluentAssertions;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.SavedSearches;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>US07 / T07.1 — saved search model &amp; alert rules.</summary>
public class SavedSearchTests
{
    private const string Student = "usr-student-01";

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // 2026-11-25 09:00 giờ VN
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 25, 2, 0, 0, TimeSpan.Zero));
    private readonly InMemoryListingRepository _listings = new();
    private readonly InMemorySavedSearchRepository _repo = new();
    private readonly SavedSearchService _service;
    private readonly SavedSearchAlertRules _rules;

    public SavedSearchTests()
    {
        _service = new SavedSearchService(_repo, _listings, _clock);
        _rules = new SavedSearchAlertRules(_repo, _listings, _clock);
    }

    private static SaveSearchRequestDto Request(decimal? maxRent = 2_000_000m, string? roomType = null, List<double[]>? polygon = null) => new()
    {
        Name = "Khu Z115 < 2tr",
        Filters = new SavedSearchFiltersDto { MaxRent = maxRent, RoomType = roomType },
        Polygon = polygon,
        AlertsEnabled = true
    };

    [Fact]
    public async Task Create_ValidRequest_PersistsAndReturnsDto()
    {
        var created = await _service.CreateAsync(Student, Request());

        created.Id.Should().NotBeNullOrEmpty();
        created.UserId.Should().Be(Student);
        created.AlertsEnabled.Should().BeTrue();
        created.MaxAlertsPerDay.Should().Be(3);
        created.AlertsSentToday.Should().Be(0);
        (await _service.GetMineAsync(Student)).Should().ContainSingle(s => s.Id == created.Id);
        (await _service.GetMineAsync("usr-other")).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_OpenPolygon_IsAutoClosed()
    {
        var created = await _service.CreateAsync(Student, Request(polygon: [[105.80, 21.58], [105.81, 21.58], [105.81, 21.59]]));

        created.Polygon.Should().HaveCount(4);
        created.Polygon![^1].Should().Equal(created.Polygon[0]);
    }

    [Fact]
    public async Task Create_InvalidRequest_ThrowsWithFieldErrors()
    {
        var request = new SaveSearchRequestDto
        {
            Name = "  ",
            Filters = new SavedSearchFiltersDto { MinRent = 3_000_000m, MaxRent = 1_000_000m },
            Polygon = [[105.80, 21.58], [105.81, 21.58]]
        };

        var act = () => _service.CreateAsync(Student, request);

        var ex = (await act.Should().ThrowAsync<RequestValidationException>()).Which;
        ex.Errors.Keys.Should().BeEquivalentTo("filters.rent", "polygon"); // tên trống không phải lỗi
    }

    [Fact]
    public async Task Create_BlankName_GetsDefaultNameInVietnamTime()
    {
        var request = Request(polygon: [[105.80, 21.58], [105.81, 21.58], [105.81, 21.59]]);
        request.Name = null;

        var created = await _service.CreateAsync(Student, request);

        created.Name.Should().Be("Khu vực đã vẽ 25/11 09:00");
    }

    [Fact]
    public async Task UpdateToggleDelete_OnlyAffectOwner()
    {
        var created = await _service.CreateAsync(Student, Request());

        (await _service.UpdateAsync("usr-other", created.Id, Request())).Should().BeNull();
        (await _service.SetAlertsAsync(Student, created.Id, false))!.AlertsEnabled.Should().BeFalse();
        (await _service.UpdateAsync(Student, created.Id, Request(maxRent: 1_500_000m)))!.Filters.MaxRent.Should().Be(1_500_000m);
        (await _service.DeleteAsync("usr-other", created.Id)).Should().BeFalse();
        (await _service.DeleteAsync(Student, created.Id)).Should().BeTrue();
        (await _service.GetAsync(Student, created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task GetMatches_AppliesFiltersAndExcludesUnavailable()
    {
        var created = await _service.CreateAsync(Student, Request(maxRent: 1_850_000m, roomType: "Studio"));

        var matches = await _service.GetMatchesAsync(Student, created.Id);

        // Seed: room-001 Studio 1.8tr (khớp); room-003 Khép kín; room-002 1.9tr; room-004 Rented.
        matches!.Items.Select(i => i.Id).Should().Equal("room-2026-001");
    }

    [Fact]
    public void Matcher_PolygonExcludesListingOutsideArea()
    {
        var search = new SavedSearchDocument
        {
            CampusId = "ictu-main",
            Polygon = [[106.0, 22.0], [106.1, 22.0], [106.1, 22.1], [106.0, 22.0]]
        };
        var listing = new ListingDocument { Status = "Available", IsVerified = true, Location = new GeoJsonPoint() };

        SavedSearchMatcher.Matches(search, listing).Should().BeFalse();
        search.Polygon = null;
        SavedSearchMatcher.Matches(search, listing).Should().BeTrue();
    }

    [Fact]
    public async Task AlertRules_MaxThreeAlertsPerDay_ThenResetsNextDay()
    {
        var created = await _service.CreateAsync(Student, Request(maxRent: null));
        string[] listingIds = ["room-2026-001", "room-2026-002", "room-2026-003", "room-2026-005"];

        var sent = 0;
        foreach (var listingId in listingIds)
        {
            var candidates = await _rules.GetAlertCandidatesAsync(listingId);
            foreach (var c in candidates.Where(c => c.SavedSearchId == created.Id))
            {
                if (await _rules.TryRecordAlertAsync(c)) sent++;
            }
        }

        sent.Should().Be(3);
        (await _rules.GetAlertCandidatesAsync("room-2026-005")).Should().BeEmpty();
        (await _service.GetAsync(Student, created.Id))!.AlertsSentToday.Should().Be(3);

        _clock.Now = _clock.Now.AddDays(1); // sang ngày mới (giờ VN) -> reset bộ đếm
        var next = await _rules.GetAlertCandidatesAsync("room-2026-005");
        next.Should().ContainSingle().Which.RemainingAlertsToday.Should().Be(2);
        (await _service.GetAsync(Student, created.Id))!.AlertsSentToday.Should().Be(0);
    }

    [Fact]
    public async Task AlertRules_SameListingIsNeverAlertedTwice()
    {
        var created = await _service.CreateAsync(Student, Request());
        var candidate = (await _rules.GetAlertCandidatesAsync("room-2026-001")).Single(c => c.SavedSearchId == created.Id);

        (await _rules.TryRecordAlertAsync(candidate)).Should().BeTrue();
        (await _rules.TryRecordAlertAsync(candidate)).Should().BeFalse();
        (await _rules.GetAlertCandidatesAsync("room-2026-001")).Should().BeEmpty();
    }

    [Fact]
    public async Task AlertRules_DisabledAlertsOrUnavailableListing_ProduceNoCandidates()
    {
        var created = await _service.CreateAsync(Student, Request(maxRent: null));
        (await _rules.GetAlertCandidatesAsync("room-2026-004")).Should().BeEmpty(); // Rented

        await _service.SetAlertsAsync(Student, created.Id, false);
        (await _rules.GetAlertCandidatesAsync("room-2026-001")).Should().BeEmpty();
    }

    [Fact]
    public void TodayInVietnam_UsesUtcPlus7()
    {
        var lateUtc = new ManualTimeProvider(new DateTimeOffset(2026, 11, 25, 17, 30, 0, TimeSpan.Zero)); // 00:30 ngày 26 giờ VN

        SavedSearchAlertRules.TodayInVietnam(lateUtc).Should().Be(new DateOnly(2026, 11, 26));
    }
}
