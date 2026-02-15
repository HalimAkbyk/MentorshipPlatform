using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class LectureNoteConfiguration : IEntityTypeConfiguration<LectureNote>
{
    public void Configure(EntityTypeBuilder<LectureNote> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => new { x.EnrollmentId, x.LectureId });

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
