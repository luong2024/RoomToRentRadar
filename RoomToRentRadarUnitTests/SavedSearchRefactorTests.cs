using FluentAssertions;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.SavedSearches;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>Refactor US07 / T07.1 — validator và mapper tách khỏi SavedSearchService.</summary>
public class SavedSearchRefactorTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);

    [Fact]
    public void Validate_NullBody_ThrowsVietnameseBodyError()
    {
        var act = () => SavedSearchRequestValidator.Validate(null);

        act.Should().Throw<RequestValidationException>()
            .Which.Errors["body"].Should().Equal("Thiếu dữ liệu tìm kiếm cần lưu.");
    }

    [Fact]
    public void Validate_NameTooLong_ReturnsNameError()
    {
        var request = new SaveSearchRequestDto { Name = new string('a', SavedSearchRequestValidator.MaxNameLength + 1) };

        var act = () => SavedSearchRequestValidator.Validate(request);

        act.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("name");
    }

    [Fact]
    public void Validate_NoPolygon_ReturnsNull()
    {
        SavedSearchRequestValidator.Validate(new SaveSearchRequestDto()).Should().BeNull();
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 0)]
    public void Mapper_AlertsSentToday_ResetsOnNewDay(int counterDayOffset, int expected)
    {
        var doc = new SavedSearchDocument { AlertsSentToday = 2, AlertCounterDate = Today.AddDays(counterDayOffset) };

        SavedSearchMapper.ToDto(doc, Today).AlertsSentToday.Should().Be(expected);
    }

    [Fact]
    public void ApplyRequest_BlankCampus_FallsBackToDefaultCampus()
    {
        var doc = new SavedSearchDocument();
        var request = new SaveSearchRequestDto
        {
            Name = "  Gần trường  ",
            Filters = new SavedSearchFiltersDto { CampusId = " ", RoomType = " " }
        };

        doc.ApplyRequest(request, polygon: null, DateTimeOffset.UnixEpoch);

        doc.Name.Should().Be("Gần trường");
        doc.CampusId.Should().Be(CampusIds.Default);
        doc.RoomType.Should().BeNull();
    }
}
