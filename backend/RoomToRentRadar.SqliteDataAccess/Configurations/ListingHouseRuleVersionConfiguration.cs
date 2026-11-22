using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.SqliteDataAccess.Configurations;

public sealed class ListingHouseRuleVersionConfiguration : IEntityTypeConfiguration<ListingHouseRuleVersion>
{
    public void Configure(EntityTypeBuilder<ListingHouseRuleVersion> builder)
    {
        builder.ToTable("listing_house_rule_versions", table =>
        {
            table.HasCheckConstraint("ck_listing_house_rule_versions_version_positive", "version >= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.QuietHours).HasColumnName("quiet_hours").HasMaxLength(300).IsRequired();
        builder.Property(x => x.GuestPolicy).HasColumnName("guest_policy").HasMaxLength(300).IsRequired();
        builder.Property(x => x.CookingRules).HasColumnName("cooking_rules").HasMaxLength(300).IsRequired();
        builder.Property(x => x.ParkingRules).HasColumnName("parking_rules").HasMaxLength(300).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.HasOne(x => x.Listing)
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ListingId, x.Version })
            .IsUnique()
            .HasDatabaseName("ix_listing_house_rule_versions_listing_version");
    }
}
