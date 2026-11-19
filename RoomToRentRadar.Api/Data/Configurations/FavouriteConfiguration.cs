using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class FavouriteConfiguration : IEntityTypeConfiguration<Favourite>
{
    public void Configure(EntityTypeBuilder<Favourite> builder)
    {
        builder.ToTable("favourites");

        builder.HasKey(x => new { x.UserId, x.ListingId });
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("ix_favourites_user_id_created_at");
        builder.HasIndex(x => x.ListingId)
            .HasDatabaseName("ix_favourites_listing_id");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Listing)
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
