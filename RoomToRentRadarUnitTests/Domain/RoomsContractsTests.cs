using System.Reflection;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>H2: request/DTO shapes must match <c>docs/api/openapi.yaml</c> (searchRooms / RoomCardDto).</summary>
[TestFixture]
public class RoomsContractsTests
{
    [Test]
    public void RoomSearchRequest_HasOpenApiParameterNamesAndDefaults()
    {
        var request = new RoomSearchRequest();

        Assert.Multiple(() =>
        {
            Assert.That(request.CampusId, Is.Null);
            Assert.That(request.MinRentVnd, Is.Null);
            Assert.That(request.MaxRentVnd, Is.Null);
            Assert.That(request.MinAreaM2, Is.Null);
            Assert.That(request.MaxAreaM2, Is.Null);
            Assert.That(request.RoomTypes, Is.Null);
            Assert.That(request.MaxDistanceKm, Is.Null);
            Assert.That(request.Sort, Is.Null);
            Assert.That(request.PageNumber, Is.EqualTo(1));
            Assert.That(request.PageSize, Is.EqualTo(20));
        });
    }

    [Test]
    public void RoomSearchRequest_DoesNotExposeOutOfScopeParameters()
    {
        var propertyNames = typeof(RoomSearchRequest).GetProperties().Select(p => p.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(propertyNames, Does.Not.Contain("Keyword"), "keyword không thuộc AC US04");
            Assert.That(propertyNames, Does.Not.Contain("Latitude"), "latitude không còn là tham số đầu vào (dùng campusId)");
            Assert.That(propertyNames, Does.Not.Contain("Longitude"));
            Assert.That(propertyNames, Does.Not.Contain("MinPrice"));
            Assert.That(propertyNames, Does.Not.Contain("MaxPrice"));
            Assert.That(propertyNames, Does.Not.Contain("Page"));
        });
    }

    [Test]
    public void RoomSearchRequest_WithExpression_ProducesClonedValue()
    {
        var request = new RoomSearchRequest(PageNumber: 1, PageSize: 20);

        var clamped = request with { PageNumber = 2, PageSize = 50 };

        Assert.Multiple(() =>
        {
            Assert.That(clamped.PageNumber, Is.EqualTo(2));
            Assert.That(clamped.PageSize, Is.EqualTo(50));
            Assert.That(request.PageNumber, Is.EqualTo(1));
        });
    }

    [Test]
    public void RoomCardDto_HasExactlyTheOpenApiProperties()
    {
        var expected = new[]
        {
            "Id", "Title", "MonthlyRentVnd", "AreaM2", "RoomType", "DistanceKm",
            "IsVerified", "Status", "CoverThumbUrl", "Latitude", "Longitude",
            "PublishedAtUtc", "IsFavourite"
        };

        var actual = typeof(RoomCardDto).GetProperties().Select(p => p.Name).ToList();

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void RoomCardDto_StoresAllValues()
    {
        var publishedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var dto = new RoomCardDto(
            Id: "room-1",
            Title: "Phòng trọ gần ICTU",
            MonthlyRentVnd: 1_500_000L,
            AreaM2: 18.0,
            RoomType: RoomType.MiniApartment,
            DistanceKm: 0.3,
            IsVerified: true,
            Status: ListingStatus.Available,
            CoverThumbUrl: "https://cdn.example/room-1.jpg",
            Latitude: 21.5875,
            Longitude: 105.8080,
            PublishedAtUtc: publishedAt,
            IsFavourite: false);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo("room-1"));
            Assert.That(dto.MonthlyRentVnd, Is.EqualTo(1_500_000L));
            Assert.That(dto.AreaM2, Is.EqualTo(18.0));
            Assert.That(dto.RoomType, Is.EqualTo(RoomType.MiniApartment));
            Assert.That(dto.DistanceKm, Is.EqualTo(0.3));
            Assert.That(dto.IsVerified, Is.True);
            Assert.That(dto.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(dto.CoverThumbUrl, Is.EqualTo("https://cdn.example/room-1.jpg"));
            Assert.That(dto.PublishedAtUtc, Is.EqualTo(publishedAt));
            Assert.That(dto.IsFavourite, Is.False);
        });
    }

    [Test]
    public void RoomSearchRequest_AllowedSortsMatchOpenApiSortBy()
    {
        Assert.That(RoomSearchRequest.AllowedSorts, Is.EquivalentTo(new[]
        {
            "distance", "price_asc", "price_desc", "newest"
        }), "SortBy enum trong openapi.yaml (BR-SRCH-04)");
        Assert.That(RoomSearchRequest.DefaultSort, Is.EqualTo("distance"));
    }

    [Test]
    public void RoomSearchRequest_PagingLimitsMatchOpenApi()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RoomSearchRequest.MaxPageSize, Is.EqualTo(50));
            Assert.That(RoomSearchRequest.MinDistanceKm, Is.EqualTo(0.1));
            Assert.That(RoomSearchRequest.MaxDistanceKmAllowed, Is.EqualTo(50.0));
        });
    }

    [Test]
    public void ListingStatus_HasAdr011Values()
    {
        Assert.That(Enum.GetNames<ListingStatus>(), Is.EquivalentTo(new[]
        {
            "PendingReview", "Rejected", "Available", "Rented", "Unpublished", "Suspended"
        }));
    }

    [Test]
    public void RoomType_HasOpenApiValues()
    {
        Assert.That(Enum.GetNames<RoomType>(), Is.EquivalentTo(new[]
        {
            "BoardingRoom", "MiniApartment", "WholeHouse", "Dormitory"
        }));
    }

    [Test]
    public async Task IRoomRepository_ExposesOnlyGetByIdAddAndUpdate()
    {
        var methodNames = typeof(IRoomRepository).GetMethods().Select(m => m.Name).ToList();

        Assert.That(methodNames, Is.EquivalentTo(new[] { "GetByIdAsync", "AddAsync", "UpdateAsync" }),
            "GetAllAsync đã bị bỏ (M2: full-scan bị thay bằng IRoomSearchReadModel)");

        var repository = Substitute.For<IRoomRepository>();
        var room = Room.Create(
            "landlord-1", "Phòng trọ Sinh viên Z115", RoomType.BoardingRoom, 1_500_000L, 0L, 18.0,
            "Z115", "Tân Thịnh", "TP. Thái Nguyên", 21.5875, 105.8080);
        repository.GetByIdAsync(room.Id, Arg.Any<CancellationToken>()).Returns(room);

        var found = await repository.GetByIdAsync(room.Id);

        Assert.That(found, Is.SameAs(room));
    }

    [Test]
    public async Task ICampusRepository_CanBeSubstitutedWithNSubstitute()
    {
        var repository = Substitute.For<ICampusRepository>();
        var ictu = new Campus("ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, true, 1);
        repository.GetDefaultCampusAsync(Arg.Any<CancellationToken>()).Returns(ictu);
        repository.GetByIdAsync("ICTU", Arg.Any<CancellationToken>()).Returns(ictu);

        var defaultCampus = await repository.GetDefaultCampusAsync();
        var byId = await repository.GetByIdAsync("ICTU");

        Assert.Multiple(() =>
        {
            Assert.That(defaultCampus, Is.SameAs(ictu));
            Assert.That(byId, Is.SameAs(ictu));
            Assert.That(typeof(ICampusRepository).GetMethods().Select(m => m.Name),
                Is.EquivalentTo(new[] { "GetByIdAsync", "GetDefaultCampusAsync", "GetAllAsync" }));
        });
    }

    [Test]
    public void IRoomSearchReadModel_ExposesSingleQueryMethod()
    {
        var methods = typeof(IRoomSearchReadModel).GetMethods();

        Assert.That(methods.Select(m => m.Name), Is.EquivalentTo(new[] { "QueryCandidatesAsync" }));
    }

    [Test]
    public async Task ISearchRoomsUseCase_CanBeSubstitutedWithNSubstitute()
    {
        var useCase = Substitute.For<ISearchRoomsUseCase>();
        var expected = new PagedResult<RoomCardDto>(Array.Empty<RoomCardDto>(), 0, 1, 20);

        useCase.ExecuteAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await useCase.ExecuteAsync(new RoomSearchRequest());

        Assert.That(result, Is.SameAs(expected));
    }
}
