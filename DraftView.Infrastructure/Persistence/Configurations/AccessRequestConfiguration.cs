using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DraftView.Infrastructure.Persistence.Configurations;

public class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        builder.ToTable("AccessRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReaderId).IsRequired();
        builder.Property(r => r.ProjectId).IsRequired();

        builder.Property(r => r.CoverNote)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(r => r.ContactEmail)
            .IsRequired(false)
            .HasMaxLength(320);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(r => r.RequestedAt).IsRequired();
        builder.Property(r => r.RespondedAt).IsRequired(false);
        builder.Property(r => r.SeenByReaderAt).IsRequired(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.ReaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.ReaderId, r.ProjectId });
        builder.HasIndex(r => r.ProjectId);
    }
}
