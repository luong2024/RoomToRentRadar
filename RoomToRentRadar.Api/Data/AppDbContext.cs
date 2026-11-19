using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();
    public DbSet<ListingAmenity> ListingAmenities => Set<ListingAmenity>();
    public DbSet<ListingRule> ListingRules => Set<ListingRule>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<SavedSearchAlertLog> SavedSearchAlertLogs => Set<SavedSearchAlertLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ListingReport> ListingReports => Set<ListingReport>();
    public DbSet<ListingFavourite> ListingFavourites => Set<ListingFavourite>();
    public DbSet<ModerationAuditLog> ModerationAuditLogs => Set<ModerationAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
