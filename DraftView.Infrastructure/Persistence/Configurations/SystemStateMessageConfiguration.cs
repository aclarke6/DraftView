using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class SystemStateMessageConfiguration : IEntityTypeConfiguration<SystemStateMessage>
{
    public void Configure(EntityTypeBuilder<SystemStateMessage> builder)
    {
        builder.ToTable("SystemStateMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.CreatedByUserId).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.DeactivatedAt).IsRequired(false);
        builder.Property(m => m.Severity).IsRequired();
    }
}
