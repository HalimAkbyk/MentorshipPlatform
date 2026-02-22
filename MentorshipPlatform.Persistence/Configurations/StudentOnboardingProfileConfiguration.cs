using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class StudentOnboardingProfileConfiguration : IEntityTypeConfiguration<StudentOnboardingProfile>
{
    public void Configure(EntityTypeBuilder<StudentOnboardingProfile> builder)
    {
        builder.ToTable("StudentOnboardingProfiles");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();
        builder.HasIndex(e => e.UserId).IsUnique();

        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Gender).HasMaxLength(50);
        builder.Property(e => e.Status).HasMaxLength(50);
        builder.Property(e => e.StatusDetail).HasMaxLength(500);
        builder.Property(e => e.Goals).HasMaxLength(1000);
        builder.Property(e => e.Categories).HasMaxLength(1000);
        builder.Property(e => e.Subtopics).HasMaxLength(2000);
        builder.Property(e => e.Level).HasMaxLength(50);
        builder.Property(e => e.Preferences).HasMaxLength(1000);
        builder.Property(e => e.Availability).HasMaxLength(1000);
        builder.Property(e => e.SessionFormats).HasMaxLength(500);
    }
}
