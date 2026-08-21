using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class TenancySubscriptionConfiguration : IEntityTypeConfiguration<TenancySubscription>
{
    public void Configure(EntityTypeBuilder<TenancySubscription> builder)
    {
        builder.ToTable("TenancySubscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenancyId)
            .IsRequired();

        builder.Property(s => s.Tier)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.ProviderSubscriptionId)
            .HasMaxLength(200);

        builder.Property(s => s.IsActive)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // One subscription record per tenancy.
        builder.HasIndex(s => s.TenancyId)
            .IsUnique();

        builder.HasOne<Tenancy>()
            .WithMany()
            .HasForeignKey(s => s.TenancyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
