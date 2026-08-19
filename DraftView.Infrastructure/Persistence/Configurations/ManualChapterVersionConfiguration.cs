using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ManualChapterVersion.
/// Versions are immutable after creation; the only removal path is hard delete.
/// </summary>
public class ManualChapterVersionConfiguration : IEntityTypeConfiguration<ManualChapterVersion>
{
    public void Configure(EntityTypeBuilder<ManualChapterVersion> builder)
    {
        builder.ToTable("ManualChapterVersions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.ManualChapterId).IsRequired();
        builder.Property(v => v.AuthorId).IsRequired();
        builder.Property(v => v.VersionNumber).IsRequired();

        builder.Property(v => v.RawContent).IsRequired();

        builder.Property(v => v.SnapshotReason)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(v => v.SnapshotAt).IsRequired();

        builder.HasIndex(v => new { v.ManualChapterId, v.VersionNumber })
            .IsUnique();
    }
}
