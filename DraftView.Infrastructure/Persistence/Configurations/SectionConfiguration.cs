using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScrivenerUuid)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => new { s.ProjectId, s.ScrivenerUuid })
            .IsUnique();

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.NodeType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.ScrivenerStatus)
            .HasMaxLength(100);

        builder.Property(s => s.HtmlContent)
            .HasColumnType("TEXT");

        builder.Property(s => s.ContentHash)
            .HasMaxLength(100);

        builder.Property(s => s.IsPublished)
            .IsRequired();

        builder.Property(s => s.IsSoftDeleted)
            .IsRequired();

        // Self-referential tree
        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(s => s.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasMany<Comment>()
            .WithOne()
            .HasForeignKey(c => c.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<ReadEvent>()
            .WithOne()
            .HasForeignKey(r => r.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
