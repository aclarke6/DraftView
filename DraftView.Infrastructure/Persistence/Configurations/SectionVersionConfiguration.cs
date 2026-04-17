using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class SectionVersionConfiguration : IEntityTypeConfiguration<SectionVersion>
{
    public void Configure(EntityTypeBuilder<SectionVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.HasIndex(v => v.SectionId);

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.HtmlContent)
            .IsRequired();

        builder.Property(v => v.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.ChangeClassification)
            .HasConversion<int?>();

        builder.Property(v => v.AiSummary)
            .HasMaxLength(500);

        builder.Property(v => v.CreatedAt)
            .IsRequired();
    }
}
