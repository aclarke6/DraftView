using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DraftView.Domain.Entities;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class ReaderAccessConfiguration : IEntityTypeConfiguration<ReaderAccess>
{
    public void Configure(EntityTypeBuilder<ReaderAccess> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReaderId).IsRequired();
        builder.Property(r => r.AuthorId).IsRequired();
        builder.Property(r => r.ProjectId).IsRequired();
        builder.Property(r => r.GrantedAt).IsRequired();

        // Unique: one access record per reader/project pair
        builder.HasIndex(r => new { r.ReaderId, r.ProjectId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.ReaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
