using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class MentorOnboardingProfileConfiguration : IEntityTypeConfiguration<MentorOnboardingProfile>
{
    public void Configure(EntityTypeBuilder<MentorOnboardingProfile> builder)
    {
        builder.ToTable("MentorOnboardingProfiles");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MentorUserId).IsRequired();
        builder.HasIndex(e => e.MentorUserId).IsUnique();

        builder.Property(e => e.MentorType).HasMaxLength(50);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Timezone).HasMaxLength(100);
        builder.Property(e => e.Languages).HasMaxLength(500);
        builder.Property(e => e.Categories).HasMaxLength(1000);
        builder.Property(e => e.Subtopics).HasMaxLength(2000);
        builder.Property(e => e.TargetAudience).HasMaxLength(1000);
        builder.Property(e => e.ExperienceLevels).HasMaxLength(500);
        builder.Property(e => e.YearsOfExperience).HasMaxLength(50);
        builder.Property(e => e.CurrentRole).HasMaxLength(200);
        builder.Property(e => e.CurrentCompany).HasMaxLength(200);
        builder.Property(e => e.PreviousCompanies).HasMaxLength(500);
        builder.Property(e => e.Education).HasMaxLength(300);
        builder.Property(e => e.Certifications).HasMaxLength(500);
        builder.Property(e => e.LinkedinUrl).HasMaxLength(500);
        builder.Property(e => e.GithubUrl).HasMaxLength(500);
        builder.Property(e => e.PortfolioUrl).HasMaxLength(500);
        builder.Property(e => e.YksExamType).HasMaxLength(50);
        builder.Property(e => e.YksScore).HasMaxLength(50);
        builder.Property(e => e.YksRanking).HasMaxLength(50);
        builder.Property(e => e.MentoringTypes).HasMaxLength(1000);
        builder.Property(e => e.SessionFormats).HasMaxLength(500);
    }
}
