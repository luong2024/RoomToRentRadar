using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Entities;

namespace RoomToRentRadar.Api.Data;

/// <summary>
/// EF Core database context for the auth API (US01 / T01.1).
/// Inherits Identity's tables (AspNetUsers, AspNetRoles, AspNetUserRoles, ...) with int keys.
/// The SQLite provider and connection string are configured in Program.cs.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must run first so Identity configures its own tables and keys.
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            user.Property(u => u.SelectedCampus).HasMaxLength(50).IsRequired();
        });
    }
}
