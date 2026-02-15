using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class LectureProgressConfiguration : IEntityTypeConfiguration<LectureProgress>
{
    public void Configure(EntityTypeBuilder<LectureProgress> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.EnrollmentId, x.LectureId }).IsUnique();

        builder.HasOne(x => x.Enrollment)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Lecture)
            .WithMany()
            .HasForeignKey(x => x.LectureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
