using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class ScrivenerProjectConfiguration : IEntityTypeConfiguration<ScrivenerProject>
{
    public void Configure(EntityTypeBuilder<ScrivenerProject> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.DropboxPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.SyncStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.IsReaderActive)
            .IsRequired();

        builder.Property(p => p.IsSoftDeleted)
            .IsRequired();

        builder.HasMany<Section>()
            .WithOne()
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
