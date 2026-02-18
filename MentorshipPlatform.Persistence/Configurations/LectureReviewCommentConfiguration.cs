using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class LectureReviewCommentConfiguration : IEntityTypeConfiguration<LectureReviewComment>
{
    public void Configure(EntityTypeBuilder<LectureReviewComment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LectureTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.VideoKey).HasMaxLength(500);
        builder.Property(x => x.Comment).HasMaxLength(2000).IsRequired();

        builder.Property(x => x.Flag)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Navigation: LectureReviewComment -> CourseLecture (nullable - lecture may be deleted)
        builder.HasOne(x => x.Lecture)
            .WithMany()
            .HasForeignKey(x => x.LectureId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ReviewRoundId);
        builder.HasIndex(x => x.LectureId);
    }
}
