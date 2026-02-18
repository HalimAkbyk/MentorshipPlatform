using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CourseReviewRoundConfiguration : IEntityTypeConfiguration<CourseReviewRound>
{
    public void Configure(EntityTypeBuilder<CourseReviewRound> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MentorNotes).HasMaxLength(2000);
        builder.Property(x => x.AdminGeneralNotes).HasMaxLength(2000);

        builder.Property(x => x.Outcome)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Navigation: CourseReviewRound -> Course
        builder.HasOne(x => x.Course)
            .WithMany(c => c.ReviewRounds)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: CourseReviewRound -> LectureReviewComments
        builder.HasMany(x => x.LectureComments)
            .WithOne(c => c.ReviewRound)
            .HasForeignKey(c => c.ReviewRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CourseId);
        builder.HasIndex(x => new { x.CourseId, x.RoundNumber });
    }
}
