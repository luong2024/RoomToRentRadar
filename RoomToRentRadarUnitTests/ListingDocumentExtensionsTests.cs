using FluentAssertions;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>Refactor US02 / T02.1 — quy tắc đọc dùng chung trên ListingDocument.</summary>
public class ListingDocumentExtensionsTests
{
    private static ListingDocument Listing(string status, bool verified) => new()
    {
        Status = status,
        IsVerified = verified,
        Photos =
        [
            new() { Url = "/b.jpg", ThumbnailUrl = string.Empty, Order = 2 },
            new() { Url = "/a.jpg", ThumbnailUrl = "/a_thumb.jpg", Order = 1 }
        ]
    };

    [Theory]
    [InlineData(ListingAvailabilityStatus.Available, true, true)]
    [InlineData("available", true, true)]
    [InlineData(ListingAvailabilityStatus.Available, false, false)]
    [InlineData(ListingAvailabilityStatus.Rented, true, false)]
    public void IsPublic_RequiresAvailableAndVerified(string status, bool verified, bool expected)
    {
        Listing(status, verified).IsPublic().Should().Be(expected);
    }

    [Fact]
    public void CoverUrls_UseFirstPhotoByOrder()
    {
        var listing = Listing(ListingAvailabilityStatus.Available, true);

        listing.CoverPhotoUrl().Should().Be("/a.jpg");
        listing.CoverThumbnailUrl().Should().Be("/a_thumb.jpg");
    }

    [Fact]
    public void CoverUrls_NoPhotos_ReturnEmpty()
    {
        var listing = new ListingDocument();

        listing.CoverPhotoUrl().Should().BeEmpty();
        listing.CoverThumbnailUrl().Should().BeEmpty();
    }
}
