using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class ListingPhotoConfiguration : IEntityTypeConfiguration<ListingPhoto>
{
    public void Configure(EntityTypeBuilder<ListingPhoto> builder)
    {
        builder.ToTable("listing_photos", table =>
        {
            table.HasCheckConstraint("ck_listing_photos_kind", "kind IN ('photo', 'panorama')");
            table.HasCheckConstraint("ck_listing_photos_width_positive", "width > 0");
            table.HasCheckConstraint("ck_listing_photos_height_positive", "height > 0");
            table.HasCheckConstraint("ck_listing_photos_sort_order_non_negative", "sort_order >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ThumbUrl).HasColumnName("thumb_url").HasMaxLength(500);
        builder.Property(x => x.Width).HasColumnName("width");
        builder.Property(x => x.Height).HasColumnName("height");
        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .HasConversion<ListingPhotoKindConverter>()
            .IsRequired();
        builder.Property(x => x.Alt).HasColumnName("alt").HasMaxLength(300);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.IsCover).HasColumnName("is_cover");

        builder.HasOne(x => x.Listing)
            .WithMany(x => x.Photos)
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ListingId, x.IsCover, x.SortOrder, x.Id })
            .HasDatabaseName("ix_listing_photos_listing_cover_sort_id")
            .IsDescending(false, true, false, false);
    }
}
