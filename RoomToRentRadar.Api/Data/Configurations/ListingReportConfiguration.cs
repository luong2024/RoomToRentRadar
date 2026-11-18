using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data.Configurations;

public sealed class ListingReportConfiguration : IEntityTypeConfiguration<ListingReport>
{
    public const int MaximumDetailsLength = 1000;

    public void Configure(EntityTypeBuilder<ListingReport> builder)
    {
        builder.ToTable("listing_reports", table =>
        {
            table.HasCheckConstraint(
                "ck_listing_reports_status",
                "status IN ('open', 'in_review', 'resolved', 'dismissed')");
            table.HasCheckConstraint(
                "ck_listing_reports_reason",
                "reason IN ('spam', 'wrong_info', 'scam', 'offensive', 'already_rented')");
            table.HasCheckConstraint(
                "ck_listing_reports_details_length",
                "details IS NULL OR length(details) <= 1000");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListingId).HasColumnName("listing_id");
        builder.Property(x => x.ReporterId).HasColumnName("reporter_id");
        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(20)
            .HasConversion<ListingReportReasonConverter>()
            .IsRequired();
        builder.Property(x => x.Details)
            .HasColumnName("details")
            .HasMaxLength(MaximumDetailsLength);
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<ListingReportStatusConverter>()
            .IsRequired();
        builder.Property(x => x.ClaimedByUserId).HasColumnName("claimed_by_user_id");
        builder.Property(x => x.ClaimedUntilUtc).HasColumnName("claimed_until_utc");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.Version).HasColumnName("version").HasDefaultValue(0L);

        builder.HasIndex(x => x.ListingId)
            .HasDatabaseName("ix_listing_reports_listing_id");
        builder.HasIndex(x => new { x.Status, x.ListingId })
            .HasDatabaseName("ix_listing_reports_status_listing_id");

        builder.HasOne(x => x.Listing)
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ClaimedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
