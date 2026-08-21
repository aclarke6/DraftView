using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Tenancy entity.
/// </summary>
public class TenancyConfiguration : IEntityTypeConfiguration<Tenancy>
{
    public void Configure(EntityTypeBuilder<Tenancy> builder)
    {
        builder.ToTable("Tenancies");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.OwnerAccountId)
            .IsRequired();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.MaxBetaReaderCount)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.IsSoftDeleted)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasIndex(t => t.OwnerAccountId)
            .IsUnique();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.OwnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
