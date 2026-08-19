using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ManualChapter.
/// Chapters are author-scoped and belong to a single project.
/// </summary>
public class ManualChapterConfiguration : IEntityTypeConfiguration<ManualChapter>
{
    public void Configure(EntityTypeBuilder<ManualChapter> builder)
    {
        builder.ToTable("ManualChapters");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.AuthorId).IsRequired();
        builder.Property(c => c.ProjectId).IsRequired();

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.SortOrder).IsRequired();

        builder.Property(c => c.RawContent)
            .IsRequired();

        builder.Property(c => c.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired(false);

        builder.Property(c => c.UploadedAt).IsRequired();

        builder.HasIndex(c => new { c.ProjectId, c.AuthorId, c.SortOrder });
    }
}
