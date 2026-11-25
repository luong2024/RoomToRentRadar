using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.Search;

[TestFixture]
public class RoomSearchCriteriaTests
{
    [Test]
    public void Default_MapsToEmptySearchRequest()
    {
        var request = RoomSearchCriteria.Default.ToRoomSearchRequest();

        Assert.That(request, Is.EqualTo(new RoomSearchRequest()));
        Assert.That(request.RoomTypes, Is.Null);
        Assert.That(request.Sort, Is.Null);
        Assert.That(RoomSearchCriteria.Default.ActiveFilterCount, Is.EqualTo(0));
        Assert.That(RoomSearchCriteria.Default.HasActiveFilters, Is.False);
    }

    [Test]
    public void ActiveFilterCount_CountsPriceAreaEachRoomTypeAndDistanceButNotSort()
    {
        var criteria = new RoomSearchCriteria
        {
            MinRentVnd = 1_000_000,
            MinAreaM2 = 15,
            RoomTypes = new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm = 3,
            Sort = "price_asc"
        };

        Assert.That(criteria.ActiveFilterCount, Is.EqualTo(5));
        Assert.That(criteria.HasActiveFilters, Is.True);
    }

    [Test]
    public void ClearFiltersPreservingCampus_KeepsCampusAndResetsEverythingElse()
    {
        var criteria = new RoomSearchCriteria
        {
            CampusId = "campus-ictu",
            CampusDisplayName = "ICTU",
            MinRentVnd = 1_000_000,
            MaxRentVnd = 3_000_000,
            MinAreaM2 = 10,
            MaxAreaM2 = 40,
            RoomTypes = new[] { RoomType.WholeHouse },
            MaxDistanceKm = 5,
            Sort = "newest"
        };

        var cleared = criteria.ClearFiltersPreservingCampus();

        Assert.That(cleared.CampusId, Is.EqualTo("campus-ictu"));
        Assert.That(cleared.CampusDisplayName, Is.EqualTo("ICTU"));
        Assert.That(cleared.HasActiveFilters, Is.False);
        Assert.That(cleared.Sort, Is.Null);
        Assert.That(cleared.RoomTypes, Is.Empty);
    }

    [Test]
    public void RemoveChip_Price_RemovesBothRentBounds()
    {
        var criteria = new RoomSearchCriteria { MinRentVnd = 1_000_000, MaxRentVnd = 2_000_000 };

        var result = criteria.RemoveChip(ActiveFilterChipKind.Price);

        Assert.That(result.MinRentVnd, Is.Null);
        Assert.That(result.MaxRentVnd, Is.Null);
    }

    [Test]
    public void RemoveChip_Area_RemovesBothAreaBounds()
    {
        var criteria = new RoomSearchCriteria { MinAreaM2 = 10, MaxAreaM2 = 30 };

        var result = criteria.RemoveChip(ActiveFilterChipKind.Area);

        Assert.That(result.MinAreaM2, Is.Null);
        Assert.That(result.MaxAreaM2, Is.Null);
    }

    [Test]
    public void RemoveChip_RoomType_RemovesOnlyThatType()
    {
        var criteria = new RoomSearchCriteria
        {
            RoomTypes = new[] { RoomType.BoardingRoom, RoomType.MiniApartment }
        };

        var result = criteria.RemoveChip(ActiveFilterChipKind.RoomType, RoomType.BoardingRoom);

        Assert.That(result.RoomTypes, Is.EqualTo(new[] { RoomType.MiniApartment }));
    }

    [Test]
    public void RemoveChip_MaxDistance_RemovesDistance()
    {
        var criteria = new RoomSearchCriteria { MaxDistanceKm = 2 };

        var result = criteria.RemoveChip(ActiveFilterChipKind.MaxDistance);

        Assert.That(result.MaxDistanceKm, Is.Null);
    }

    [Test]
    public void ToRoomSearchRequest_MapsEveryFilterAndPaging()
    {
        var criteria = new RoomSearchCriteria
        {
            CampusId = "campus-ictu",
            MinRentVnd = 1_500_000,
            MaxRentVnd = 3_000_000,
            MinAreaM2 = 15,
            MaxAreaM2 = 40,
            RoomTypes = new[] { RoomType.BoardingRoom, RoomType.MiniApartment },
            MaxDistanceKm = 3,
            Sort = "price_asc"
        };

        var request = criteria.ToRoomSearchRequest(pageNumber: 2, pageSize: 50);

        Assert.That(request.CampusId, Is.EqualTo("campus-ictu"));
        Assert.That(request.MinRentVnd, Is.EqualTo(1_500_000));
        Assert.That(request.MaxRentVnd, Is.EqualTo(3_000_000));
        Assert.That(request.MinAreaM2, Is.EqualTo(15));
        Assert.That(request.MaxAreaM2, Is.EqualTo(40));
        Assert.That(request.RoomTypes, Is.EqualTo(new[] { RoomType.BoardingRoom, RoomType.MiniApartment }));
        Assert.That(request.MaxDistanceKm, Is.EqualTo(3));
        Assert.That(request.Sort, Is.EqualTo("price_asc"));
        Assert.That(request.PageNumber, Is.EqualTo(2));
        Assert.That(request.PageSize, Is.EqualTo(50));
    }

    [Test]
    public void ToRoomSearchRequest_ExplicitDistanceSort_IsNormalizedToNull()
    {
        var criteria = new RoomSearchCriteria { Sort = RoomSearchRequest.DefaultSort };

        var request = criteria.ToRoomSearchRequest();

        Assert.That(request.Sort, Is.Null);
        Assert.That(criteria.EffectiveSort, Is.EqualTo("distance"));
    }

    [Test]
    public void ToRoomSearchRequest_EmptyRoomTypes_AreNotSent()
    {
        var request = new RoomSearchCriteria { RoomTypes = Array.Empty<RoomType>() }.ToRoomSearchRequest();

        Assert.That(request.RoomTypes, Is.Null);
    }

    [Test]
    public void IsEquivalentTo_IgnoresRoomTypeOrderButDetectsValueChanges()
    {
        var a = new RoomSearchCriteria
        {
            RoomTypes = new[] { RoomType.BoardingRoom, RoomType.Dormitory }
        };
        var reordered = a with { RoomTypes = new[] { RoomType.Dormitory, RoomType.BoardingRoom } };
        var changed = a with { MaxDistanceKm = 1 };

        Assert.That(a.IsEquivalentTo(reordered), Is.True);
        Assert.That(a.IsEquivalentTo(changed), Is.False);
        Assert.That(a.IsEquivalentTo(null), Is.False);
    }
}
