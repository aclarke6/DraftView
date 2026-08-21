using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the TenancyMembership entity.
/// </summary>
public class TenancyMembershipConfiguration : IEntityTypeConfiguration<TenancyMembership>
{
    public void Configure(EntityTypeBuilder<TenancyMembership> builder)
    {
        builder.ToTable("TenancyMemberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenancyId)
            .IsRequired();

        builder.Property(m => m.AccountId)
            .IsRequired();

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.JoinedAt)
            .IsRequired();

        builder.Property(m => m.IsSoftDeleted)
            .IsRequired();

        builder.HasIndex(m => new { m.TenancyId, m.AccountId })
            .IsUnique();

        builder.HasIndex(m => m.AccountId);
    }
}
