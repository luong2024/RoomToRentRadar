using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.SqliteDataAccess.Converters;

namespace RoomToRentRadar.SqliteDataAccess.Configurations;

public sealed class RegistrationChallengeConfiguration
    : IEntityTypeConfiguration<RegistrationChallenge>
{
    public void Configure(EntityTypeBuilder<RegistrationChallenge> builder)
    {
        builder.ToTable("registration_challenges", table =>
        {
            table.HasCheckConstraint(
                "ck_registration_challenges_role",
                "role IN ('student', 'landlord')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(AccountRules.EmailMaxLength).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(AccountRules.DisplayNameMaxLength).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(AccountRules.PasswordHashMaxLength).IsRequired();
        builder.Property(x => x.PasswordSalt).HasColumnName("password_salt").HasMaxLength(AccountRules.PasswordSaltMaxLength).IsRequired();
        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasConversion<UserRoleConverter>()
            .IsRequired();
        builder.Property(x => x.OtpHash).HasColumnName("otp_hash").HasMaxLength(AccountRules.OtpHashMaxLength).IsRequired();
        builder.Property(x => x.OtpSalt).HasColumnName("otp_salt").HasMaxLength(AccountRules.OtpSaltMaxLength).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.Email)
            .HasDatabaseName("ix_registration_challenges_email");
    }
}
