using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.SqliteDataAccess.Converters;

namespace RoomToRentRadar.SqliteDataAccess.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint(
                "ck_users_role",
                "role IN ('student', 'landlord', 'moderator', 'admin')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(AccountRules.EmailMaxLength).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(AccountRules.DisplayNameMaxLength).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PasswordSalt).HasColumnName("password_salt").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasConversion<UserRoleConverter>()
            .HasDefaultValue(UserRole.Student)
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");
    }
}
