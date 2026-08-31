using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.HasKey(p => p.Id);

        builder.ToTable("UserPreferences");

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.Property(p => p.DisplayTheme)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.NotifyOnReply)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.AuthorDigestMode)
            .HasConversion<string>();

        builder.Property(p => p.AuthorTimezone)
            .HasMaxLength(100);

        builder.Property(p => p.ProseFont)
            .IsRequired();

        builder.Property(p => p.ProseFontSize)
            .IsRequired();

        builder.Property(p => p.ReaderBio)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(p => p.ReaderGenreInterests)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(p => p.ReaderPace)
            .IsRequired(false)
            .HasConversion<string>();

        builder.Property(p => p.ShowDiffOnRevisit)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.ReadingStyle)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(ReadingStyle.StoryReader);

        builder.Property(p => p.DiffCooldownHours)
            .IsRequired()
            .HasDefaultValue(24);
    }
}