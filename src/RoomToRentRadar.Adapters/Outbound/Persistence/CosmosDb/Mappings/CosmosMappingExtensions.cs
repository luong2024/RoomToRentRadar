namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Mappings;

using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

public static class CosmosMappingExtensions
{
    public static RoomListing ToDomain(this RoomListingDocument doc)
    {
        return new RoomListing(
            id: doc.Id,
            title: doc.Title,
            description: doc.Description,
            monthlyRent: new Money(doc.MonthlyRentAmount, doc.MonthlyRentCurrency),
            areaSquareMeters: doc.AreaSquareMeters,
            address: doc.Address,
            location: new GeoLocation(doc.Latitude, doc.Longitude),
            status: (RoomStatus)doc.Status,
            verificationStatus: (VerificationStatus)doc.VerificationStatus,
            campusId: doc.CampusId,
            thumbnailUrl: doc.ThumbnailUrl,
            createdAtUtc: doc.CreatedAtUtc);
    }

    public static RoomListingDocument ToDocument(this RoomListing entity)
    {
        return new RoomListingDocument
        {
            Id = entity.Id,
            CampusId = entity.CampusId,
            Title = entity.Title,
            Description = entity.Description,
            MonthlyRentAmount = entity.MonthlyRent.Amount,
            MonthlyRentCurrency = entity.MonthlyRent.Currency,
            AreaSquareMeters = entity.AreaSquareMeters,
            Address = entity.Address,
            Latitude = entity.Location.Latitude,
            Longitude = entity.Location.Longitude,
            Status = (int)entity.Status,
            VerificationStatus = (int)entity.VerificationStatus,
            ThumbnailUrl = entity.ThumbnailUrl,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public static Campus ToDomain(this CampusDocument doc)
    {
        return new Campus(
            id: doc.Id,
            name: doc.Name,
            code: doc.Code,
            location: new GeoLocation(doc.Latitude, doc.Longitude),
            address: doc.Address);
    }

    public static CampusDocument ToDocument(this Campus entity)
    {
        return new CampusDocument
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Latitude = entity.Location.Latitude,
            Longitude = entity.Location.Longitude,
            Address = entity.Address
        };
    }
}
