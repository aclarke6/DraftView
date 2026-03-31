using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class ReadEventConfiguration : IEntityTypeConfiguration<ReadEvent>
{
    public void Configure(EntityTypeBuilder<ReadEvent> builder)
    {
        builder.HasKey(r => r.Id);

        // I-11: unique per (SectionId, UserId)
        builder.HasIndex(r => new { r.SectionId, r.UserId })
            .IsUnique();

        builder.Property(r => r.FirstOpenedAt)
            .IsRequired();

        builder.Property(r => r.LastOpenedAt)
            .IsRequired();

        builder.Property(r => r.OpenCount)
            .IsRequired();
    }
}
