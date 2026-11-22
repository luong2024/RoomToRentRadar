using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.SavedSearches;
using RoomToRentRadar.SqliteDataAccess.Converters;

namespace RoomToRentRadar.SqliteDataAccess.Configurations;

public sealed class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.ToTable("saved_searches", table =>
        {
            table.HasCheckConstraint(
                "ck_saved_searches_alert_state",
                "alert_state IN ('off', 'active', 'paused')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(SavedSearchLimits.MaximumNameLength)
            .IsRequired();
        builder.Property(x => x.AreaJson).HasColumnName("area_json").IsRequired();
        builder.Property(x => x.AlertState)
            .HasColumnName("alert_state")
            .HasMaxLength(10)
            .HasConversion<SavedSearchAlertStateConverter>()
            .IsRequired();
        builder.Property(x => x.AlertsActivatedAt).HasColumnName("alerts_activated_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_saved_searches_user_id");
        builder.HasIndex(x => x.AlertState)
            .HasDatabaseName("ix_saved_searches_alert_state");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AlertLogs)
            .WithOne(x => x.SavedSearch)
            .HasForeignKey(x => x.SavedSearchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
