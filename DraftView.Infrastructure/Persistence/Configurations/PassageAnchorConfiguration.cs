using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures persistence for passage anchors and their owned snapshot and match values.
/// </summary>
public class PassageAnchorConfiguration : IEntityTypeConfiguration<PassageAnchor>
{
    /// <summary>
    /// Maps the passage anchor aggregate to additive relational columns.
    /// </summary>
    public void Configure(EntityTypeBuilder<PassageAnchor> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.SectionId);
        builder.HasIndex(a => a.OriginalSectionVersionId);
        builder.HasIndex(a => a.Purpose);
        builder.HasIndex(a => a.CreatedByUserId);

        builder.Property(a => a.Purpose)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(a => a.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SectionVersion>()
            .WithMany()
            .HasForeignKey(a => a.OriginalSectionVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.OwnsOne(a => a.OriginalSnapshot, snapshot =>
        {
            snapshot.Property(s => s.SelectedText)
                .HasColumnName("OriginalSelectedText")
                .IsRequired()
                .HasColumnType("TEXT");

            snapshot.Property(s => s.NormalizedSelectedText)
                .HasColumnName("OriginalNormalizedSelectedText")
                .IsRequired()
                .HasColumnType("TEXT");

            snapshot.Property(s => s.SelectedTextHash)
                .HasColumnName("OriginalSelectedTextHash")
                .IsRequired()
                .HasMaxLength(128);

            snapshot.Property(s => s.PrefixContext)
                .HasColumnName("OriginalPrefixContext")
                .IsRequired()
                .HasColumnType("TEXT");

            snapshot.Property(s => s.SuffixContext)
                .HasColumnName("OriginalSuffixContext")
                .IsRequired()
                .HasColumnType("TEXT");

            snapshot.Property(s => s.StartOffset)
                .HasColumnName("OriginalStartOffset")
                .IsRequired();

            snapshot.Property(s => s.EndOffset)
                .HasColumnName("OriginalEndOffset")
                .IsRequired();

            snapshot.Property(s => s.CanonicalContentHash)
                .HasColumnName("OriginalCanonicalContentHash")
                .IsRequired()
                .HasMaxLength(128);

            snapshot.Property(s => s.HtmlSelectorHint)
                .HasColumnName("OriginalHtmlSelectorHint")
                .HasColumnType("TEXT");
        });

        builder.Navigation(a => a.OriginalSnapshot)
            .IsRequired();

        builder.OwnsOne(a => a.CurrentMatch, match =>
        {
            match.Property(m => m.TargetSectionVersionId)
                .HasColumnName("CurrentTargetSectionVersionId");

            match.Property(m => m.StartOffset)
                .HasColumnName("CurrentStartOffset");

            match.Property(m => m.EndOffset)
                .HasColumnName("CurrentEndOffset");

            match.Property(m => m.MatchedText)
                .HasColumnName("CurrentMatchedText")
                .HasColumnType("TEXT");

            match.Property(m => m.ConfidenceScore)
                .HasColumnName("CurrentConfidenceScore");

            match.Property(m => m.MatchMethod)
                .HasColumnName("CurrentMatchMethod")
                .HasConversion<string>();

            match.Property(m => m.ResolvedAt)
                .HasColumnName("CurrentResolvedAt");

            match.Property(m => m.ResolvedByUserId)
                .HasColumnName("CurrentResolvedByUserId");

            match.Property(m => m.Reason)
                .HasColumnName("CurrentReason")
                .HasColumnType("TEXT");
        });

        builder.OwnsOne(a => a.Rejection, rejection =>
        {
            rejection.Property(r => r.TargetSectionVersionId)
                .HasColumnName("RejectedTargetSectionVersionId");

            rejection.Property(r => r.RejectedByUserId)
                .HasColumnName("RejectedByUserId");

            rejection.Property(r => r.RejectedAt)
                .HasColumnName("RejectedAt");

            rejection.Property(r => r.Reason)
                .HasColumnName("RejectedReason")
                .HasColumnType("TEXT");
        });

        builder.Navigation(a => a.Rejection)
            .IsRequired(false);
    }
}
