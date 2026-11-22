using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.SqliteDataAccess.Configurations;

public sealed class ListingHouseRulesConfiguration : IEntityTypeConfiguration<ListingHouseRules>
{
    public void Configure(EntityTypeBuilder<ListingHouseRules> builder)
    {
        builder.ToTable("listing_house_rules", table =>
        {
            table.HasCheckConstraint("ck_listing_house_rules_version_positive", "version >= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.QuietHours).HasColumnName("quiet_hours").HasMaxLength(300).IsRequired();
        builder.Property(x => x.GuestPolicy).HasColumnName("guest_policy").HasMaxLength(300).IsRequired();
        builder.Property(x => x.CookingRules).HasColumnName("cooking_rules").HasMaxLength(300).IsRequired();
        builder.Property(x => x.ParkingRules).HasColumnName("parking_rules").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");

        builder.HasOne(x => x.Listing)
            .WithOne(x => x.HouseRules)
            .HasForeignKey<ListingHouseRules>(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ListingId)
            .IsUnique()
            .HasDatabaseName("ix_listing_house_rules_listing");
    }
}
