using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class ReaderSnapshotConfiguration : IEntityTypeConfiguration<ReaderSnapshot>
{
    public void Configure(EntityTypeBuilder<ReaderSnapshot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.SectionId, s.UserId })
            .IsUnique();

        builder.HasIndex(s => s.SectionId);

        builder.Property(s => s.HtmlContent)
            .IsRequired();

        builder.Property(s => s.SnapshotAt)
            .IsRequired();
    }
}
