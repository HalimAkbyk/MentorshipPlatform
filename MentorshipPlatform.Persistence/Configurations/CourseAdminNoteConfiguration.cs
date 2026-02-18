using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CourseAdminNoteConfiguration : IEntityTypeConfiguration<CourseAdminNote>
{
    public void Configure(EntityTypeBuilder<CourseAdminNote> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.LectureTitle).HasMaxLength(200);

        builder.Property(x => x.NoteType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Flag)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Lecture)
            .WithMany()
            .HasForeignKey(x => x.LectureId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CourseId);
        builder.HasIndex(x => x.LectureId);
    }
}
