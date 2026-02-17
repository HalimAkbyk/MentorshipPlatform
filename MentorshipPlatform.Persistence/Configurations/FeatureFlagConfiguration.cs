using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Persistence.Configurations;

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("FeatureFlags");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).IsRequired().HasMaxLength(200);
        builder.HasIndex(e => e.Key).IsUnique();
        builder.Property(e => e.Description).HasMaxLength(1000);
    }
}
