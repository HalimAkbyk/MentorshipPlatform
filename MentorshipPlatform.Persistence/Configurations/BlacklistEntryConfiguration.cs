using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Persistence.Configurations;

public class BlacklistEntryConfiguration : IEntityTypeConfiguration<BlacklistEntry>
{
    public void Configure(EntityTypeBuilder<BlacklistEntry> builder)
    {
        builder.ToTable("BlacklistEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Value).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Reason).HasMaxLength(1000);
    }
}
