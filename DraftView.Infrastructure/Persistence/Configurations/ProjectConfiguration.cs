using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures persistence for Project, including sync status conversion and optional
/// Dropbox webhook control fields.
/// </summary>
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.AuthorId)
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.DropboxPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.DropboxCursor)
            .IsRequired(false);

        builder.Property(p => p.LastBackgroundSyncOutcome)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(p => p.SyncStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.IsReaderActive)
            .IsRequired();

        builder.Property(p => p.IsSoftDeleted)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<Section>()
            .WithOne()
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
