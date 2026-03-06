using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class VideoWatchLogConfiguration : IEntityTypeConfiguration<VideoWatchLog>
{
    public void Configure(EntityTypeBuilder<VideoWatchLog> builder)
    {
        builder.ToTable("VideoWatchLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CompletionPercentage).HasPrecision(5, 2);

        builder.HasOne(e => e.Lecture)
            .WithMany()
            .HasForeignKey(e => e.LectureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LectureId, e.StudentId });
        builder.HasIndex(e => e.InstructorId);
    }
}
