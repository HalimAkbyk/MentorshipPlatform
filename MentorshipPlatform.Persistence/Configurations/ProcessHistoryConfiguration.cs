using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class ProcessHistoryConfiguration : IEntityTypeConfiguration<ProcessHistory>
{
    public void Configure(EntityTypeBuilder<ProcessHistory> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.EntityId)
            .IsRequired();

        builder.Property(p => p.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.OldValue)
            .HasMaxLength(200);

        builder.Property(p => p.NewValue)
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(p => p.PerformedByRole)
            .HasMaxLength(50);

        builder.Property(p => p.Metadata)
            .HasColumnType("text");

        builder.HasIndex(p => new { p.EntityType, p.EntityId });
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => p.PerformedBy);
    }
}
