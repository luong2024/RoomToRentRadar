using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class ListingAmenityConfiguration : IEntityTypeConfiguration<ListingAmenity>
{
    public void Configure(EntityTypeBuilder<ListingAmenity> builder)
    {
        builder.ToTable("listing_amenities", table =>
        {
            table.HasCheckConstraint(
                "ck_listing_amenities_code",
                "code IN ('wifi', 'air_conditioner', 'parking', 'washing_machine', 'refrigerator', " +
                "'kitchen', 'private_bathroom', 'balcony', 'elevator', 'security', 'gym', 'pool', " +
                "'pet_allowed')");
            table.HasCheckConstraint(
                "ck_listing_amenities_sort_order_non_negative",
                "sort_order >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .HasConversion<AmenityCodeConverter>()
            .IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.HasOne(x => x.Listing)
            .WithMany(x => x.Amenities)
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ListingId, x.Code })
            .HasDatabaseName("ix_listing_amenities_listing_code")
            .IsUnique();

        builder.HasIndex(x => new { x.ListingId, x.SortOrder, x.Id })
            .HasDatabaseName("ix_listing_amenities_listing_sort_order_id");
    }
}
