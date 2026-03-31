using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class UserNotificationPreferencesConfiguration : IEntityTypeConfiguration<UserNotificationPreferences>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreferences> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.Property(p => p.NotifyOnReply)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.AuthorDigestMode)
            .HasConversion<string>();

        builder.Property(p => p.AuthorTimezone)
            .HasMaxLength(100);
    }
}
