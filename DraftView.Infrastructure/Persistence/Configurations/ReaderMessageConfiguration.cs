using DraftView.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class ReaderMessageConfiguration : IEntityTypeConfiguration<ReaderMessage>
{
    public void Configure(EntityTypeBuilder<ReaderMessage> builder)
    {
        builder.ToTable("ReaderMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.AuthorId).IsRequired();
        builder.Property(m => m.RecipientId).IsRequired();

        builder.Property(m => m.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Body)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.SentAt).IsRequired();

        builder.HasIndex(m => m.AuthorId);
    }
}
