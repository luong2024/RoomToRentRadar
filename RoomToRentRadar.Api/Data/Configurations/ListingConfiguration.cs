using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings", table =>
        {
            table.HasCheckConstraint("ck_listings_price_non_negative", "price >= 0");
            table.HasCheckConstraint("ck_listings_area_non_negative", "area_m2 >= 0");
            table.HasCheckConstraint("ck_listings_bedrooms_non_negative", "bedrooms >= 0");
            table.HasCheckConstraint(
                "ck_listings_status",
                "status IN ('draft', 'published', 'hidden', 'removed')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Price).HasColumnName("price");
        builder.Property(x => x.Deposit).HasColumnName("deposit");
        builder.Property(x => x.AreaM2).HasColumnName("area_m2");
        builder.Property(x => x.RoomType)
            .HasColumnName("room_type")
            .HasMaxLength(20)
            .HasConversion<RoomTypeConverter>()
            .IsRequired();
        builder.Property(x => x.Bedrooms).HasColumnName("bedrooms");
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Lat).HasColumnName("lat");
        builder.Property(x => x.Lng).HasColumnName("lng");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<ListingStatusConverter>()
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.LandlordName).HasColumnName("landlord_name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.LandlordPhone).HasColumnName("landlord_phone").HasMaxLength(20).IsRequired();

        // Each index matches one whitelisted sort: equality on status, then the
        // sort key in the requested direction, then id as the unique tie-breaker.
        builder.HasIndex(x => new { x.Status, x.CreatedAt, x.Id })
            .HasDatabaseName("ix_listings_status_created_at_id")
            .IsDescending(false, true, false);

        // EF collapses indexes with the same properties, so the ascending price
        // index (ix_listings_status_price_id) is created explicitly in the
        // initial migration.
        builder.HasIndex(x => new { x.Status, x.Price, x.Id })
            .HasDatabaseName("ix_listings_status_price_desc_id")
            .IsDescending(false, true, false);

        builder.HasIndex(x => new { x.Status, x.AreaM2, x.Id })
            .HasDatabaseName("ix_listings_status_area_m2_desc_id")
            .IsDescending(false, true, false);

        builder.HasIndex(x => new { x.Status, x.Bedrooms })
            .HasDatabaseName("ix_listings_status_bedrooms");

        // Area search is a bounding-box range scan on the two coordinates after the
        // status equality, so the leading column matches the published filter. The
        // lat/lng index serves range queries on both axes; the lng-only index covers
        // narrow longitude slices where the planner prefers a single range column.
        builder.HasIndex(x => new { x.Status, x.Lat, x.Lng })
            .HasDatabaseName("ix_listings_status_lat_lng");

        builder.HasIndex(x => new { x.Status, x.Lng })
            .HasDatabaseName("ix_listings_status_lng");
    }
}
