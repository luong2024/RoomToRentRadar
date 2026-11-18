using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class ModerationAuditLogConfiguration : IEntityTypeConfiguration<ModerationAuditLog>
{
    public const int MaximumNoteLength = 1000;

    public void Configure(EntityTypeBuilder<ModerationAuditLog> builder)
    {
        builder.ToTable("moderation_audit_logs", table =>
        {
            table.HasCheckConstraint(
                "ck_moderation_audit_logs_action",
                "action IN ('claim', 'release', 'resolve')");
            table.HasCheckConstraint(
                "ck_moderation_audit_logs_resolution_action",
                "resolution_action IS NULL OR resolution_action IN "
                + "('dismiss', 'hide_listing', 'remove_listing', 'warn_owner')");
            table.HasCheckConstraint(
                "ck_moderation_audit_logs_previous_status",
                "previous_report_status IS NULL OR previous_report_status IN "
                + "('open', 'in_review', 'resolved', 'dismissed')");
            table.HasCheckConstraint(
                "ck_moderation_audit_logs_next_status",
                "next_report_status IS NULL OR next_report_status IN "
                + "('open', 'in_review', 'resolved', 'dismissed')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(20)
            .HasConversion<ModerationAuditActionConverter>()
            .IsRequired();
        builder.Property(x => x.ResolutionAction)
            .HasColumnName("resolution_action")
            .HasMaxLength(30)
            .HasConversion<ModerationResolutionActionConverter>();
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.ReportId).HasColumnName("report_id");
        builder.Property(x => x.PreviousReportStatus)
            .HasColumnName("previous_report_status")
            .HasMaxLength(20)
            .HasConversion<ListingReportStatusConverter>();
        builder.Property(x => x.NextReportStatus)
            .HasColumnName("next_report_status")
            .HasMaxLength(20)
            .HasConversion<ListingReportStatusConverter>();
        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(MaximumNoteLength);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => new { x.ListingId, x.CreatedAt })
            .HasDatabaseName("ix_moderation_audit_logs_listing_created_at");
        builder.HasIndex(x => new { x.ActorUserId, x.CreatedAt })
            .HasDatabaseName("ix_moderation_audit_logs_actor_created_at");

        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ListingReport>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
