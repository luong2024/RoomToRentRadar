using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.Search;

[TestFixture]
public class SearchCriteriaStoreTests
{
    [Test]
    public void Apply_NewCriteria_UpdatesCurrentAndRaisesEventOnce()
    {
        var store = new SearchCriteriaStore();
        var events = new List<RoomSearchCriteria>();
        store.CriteriaChanged += (_, criteria) => events.Add(criteria);

        var criteria = new RoomSearchCriteria { MaxRentVnd = 2_000_000 };
        store.Apply(criteria);

        Assert.That(store.Current, Is.SameAs(criteria));
        Assert.That(events, Has.Count.EqualTo(1));
    }

    [Test]
    public void Apply_EquivalentCriteria_DoesNotRaiseEvent()
    {
        var store = new SearchCriteriaStore();
        var raised = 0;
        store.CriteriaChanged += (_, _) => raised++;

        store.Apply(new RoomSearchCriteria { RoomTypes = new[] { RoomType.BoardingRoom, RoomType.Dormitory } });
        store.Apply(new RoomSearchCriteria { RoomTypes = new[] { RoomType.Dormitory, RoomType.BoardingRoom } });

        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void ClearFilters_KeepsCampusAndRaisesEvent()
    {
        var store = new SearchCriteriaStore();
        store.SetCampus("campus-ictu", "ICTU");
        store.Apply(store.Current with { MaxRentVnd = 1_000_000, Sort = "newest" });

        var raised = 0;
        store.CriteriaChanged += (_, _) => raised++;
        store.ClearFilters();

        Assert.That(store.Current.CampusId, Is.EqualTo("campus-ictu"));
        Assert.That(store.Current.HasActiveFilters, Is.False);
        Assert.That(store.Current.Sort, Is.Null);
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void ClearFilters_WhenAlreadyDefault_DoesNotRaiseEvent()
    {
        var store = new SearchCriteriaStore();
        var raised = 0;
        store.CriteriaChanged += (_, _) => raised++;

        store.ClearFilters();

        Assert.That(raised, Is.EqualTo(0));
    }

    [Test]
    public void RemoveChip_RemovesOnlyThatFilter()
    {
        var store = new SearchCriteriaStore();
        store.Apply(new RoomSearchCriteria
        {
            MaxRentVnd = 2_000_000,
            RoomTypes = new[] { RoomType.BoardingRoom }
        });

        store.RemoveChip(ActiveFilterChipKind.Price);

        Assert.That(store.Current.MaxRentVnd, Is.Null);
        Assert.That(store.Current.RoomTypes, Is.EqualTo(new[] { RoomType.BoardingRoom }));
    }

    [Test]
    public void SetSort_Distance_IsNormalizedToNullWithoutEvent()
    {
        var store = new SearchCriteriaStore();
        var raised = 0;
        store.CriteriaChanged += (_, _) => raised++;

        store.SetSort("distance");

        Assert.That(store.Current.Sort, Is.Null);
        Assert.That(raised, Is.EqualTo(0));
    }

    [Test]
    public void SetSort_OtherOption_UpdatesAndRaises()
    {
        var store = new SearchCriteriaStore();
        var raised = 0;
        store.CriteriaChanged += (_, _) => raised++;

        store.SetSort("price_desc");

        Assert.That(store.Current.Sort, Is.EqualTo("price_desc"));
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void SetCampus_UpdatesCampusOnly()
    {
        var store = new SearchCriteriaStore();
        store.Apply(store.Current with { MaxDistanceKm = 3 });

        store.SetCampus("campus-tnu", "TNU");

        Assert.That(store.Current.CampusId, Is.EqualTo("campus-tnu"));
        Assert.That(store.Current.CampusDisplayName, Is.EqualTo("TNU"));
        Assert.That(store.Current.MaxDistanceKm, Is.EqualTo(3));
    }
}
