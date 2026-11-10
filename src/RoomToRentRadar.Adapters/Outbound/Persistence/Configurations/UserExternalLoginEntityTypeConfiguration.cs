using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.Configurations;

public sealed class UserExternalLoginEntityTypeConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("UserExternalLogins");

        builder.HasKey(el => el.Id);

        builder.Property(el => el.Id)
            .ValueGeneratedNever();

        builder.Property(el => el.UserId)
            .IsRequired();

        builder.Property(el => el.Provider)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(el => el.ProviderSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(el => el.ProviderEmail)
            .HasConversion(
                email => email.Value,
                value => EmailAddress.Create(value))
            .HasMaxLength(EmailAddress.MaxLength)
            .IsRequired();

        builder.Property(el => el.CreatedAt)
            .IsRequired();

        builder.HasIndex(el => new { el.Provider, el.ProviderSubject })
            .IsUnique()
            .HasDatabaseName("IX_UserExternalLogins_Provider_ProviderSubject");

        builder.HasIndex(el => el.UserId)
            .HasDatabaseName("IX_UserExternalLogins_UserId");
    }
}
