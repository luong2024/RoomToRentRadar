using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class ListingRuleConfiguration : IEntityTypeConfiguration<ListingRule>
{
    public void Configure(EntityTypeBuilder<ListingRule> builder)
    {
        builder.ToTable("listing_rules", table =>
        {
            table.HasCheckConstraint("ck_listing_rules_sort_order_non_negative", "sort_order >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.Text).HasColumnName("text").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.HasOne(x => x.Listing)
            .WithMany(x => x.Rules)
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ListingId, x.SortOrder, x.Id })
            .HasDatabaseName("ix_listing_rules_listing_sort_order_id");
    }
}
