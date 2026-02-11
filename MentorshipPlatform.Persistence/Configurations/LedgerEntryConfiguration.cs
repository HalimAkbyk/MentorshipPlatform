using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.AccountType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.Direction)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(l => l.Amount)
            .HasPrecision(18, 2);

        builder.Property(l => l.Currency)
            .HasMaxLength(3);

        builder.Property(l => l.ReferenceType)
            .HasMaxLength(50);

        builder.HasIndex(l => l.AccountOwnerUserId);
        builder.HasIndex(l => new { l.ReferenceType, l.ReferenceId });
        builder.HasIndex(l => l.CreatedAt);
    }
}