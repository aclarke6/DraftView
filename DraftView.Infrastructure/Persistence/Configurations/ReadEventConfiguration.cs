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

        builder.HasIndex(r => r.ResumeAnchorId);

        builder.Property(r => r.FirstOpenedAt)
            .IsRequired();

        builder.Property(r => r.LastOpenedAt)
            .IsRequired();

        builder.Property(r => r.OpenCount)
            .IsRequired();

        builder.Property(e => e.BannerDismissedAtVersion)
            .HasColumnName("BannerDismissedAtVersion")
            .IsRequired(false);

        builder.HasOne<PassageAnchor>()
            .WithMany()
            .HasForeignKey(r => r.ResumeAnchorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
