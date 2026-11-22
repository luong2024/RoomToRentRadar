using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.SqliteDataAccess.Configurations;

public sealed class SavedSearchAlertLogConfiguration : IEntityTypeConfiguration<SavedSearchAlertLog>
{
    public void Configure(EntityTypeBuilder<SavedSearchAlertLog> builder)
    {
        builder.ToTable("saved_search_alert_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SavedSearchId).HasColumnName("saved_search_id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.Delivered).HasColumnName("delivered");

        builder.HasIndex(x => new { x.SavedSearchId, x.ListingId })
            .IsUnique()
            .HasDatabaseName("ux_saved_search_alert_logs_search_listing");
        builder.HasIndex(x => new { x.SavedSearchId, x.CreatedAt })
            .HasDatabaseName("ix_saved_search_alert_logs_search_created_at");

        builder.HasOne(x => x.SavedSearch)
            .WithMany(x => x.AlertLogs)
            .HasForeignKey(x => x.SavedSearchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
