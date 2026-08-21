using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Account entity.
/// Mirrors the email-protection column layout used by User to support the same
/// DbContext-level encryption hook pattern.
/// </summary>
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EmailCiphertext)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(a => a.EmailLookupHmac)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(a => a.EmailLookupHmac)
            .IsUnique();

        builder.Property(a => a.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.IsActive)
            .IsRequired();

        builder.Property(a => a.IsSoftDeleted)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Ignore(a => a.Email);
    }
}
