using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Token)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(i => i.Token)
            .IsUnique();

        builder.HasIndex(i => i.UserId)
            .IsUnique();

        builder.Property(i => i.ExpiryPolicy)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(i => i.IssuedAt)
            .IsRequired();
    }
}
