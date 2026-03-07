using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(5000);

        builder.Property(x => x.Instructions)
            .HasMaxLength(5000);

        builder.Property(x => x.AssignmentType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.DifficultyLevel)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MentorUserId);
    }
}

public class AssignmentMaterialConfiguration : IEntityTypeConfiguration<AssignmentMaterial>
{
    public void Configure(EntityTypeBuilder<AssignmentMaterial> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Assignment)
            .WithMany(a => a.Materials)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LibraryItem)
            .WithMany()
            .HasForeignKey(x => x.LibraryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AssignmentId);
    }
}

public class AssignmentSubmissionConfiguration : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmission> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubmissionText)
            .HasMaxLength(5000);

        builder.Property(x => x.FileUrl)
            .HasMaxLength(500);

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(300);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Assignment)
            .WithMany(a => a.Submissions)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AssignmentId);
        builder.HasIndex(x => x.StudentUserId);
    }
}

public class SubmissionReviewConfiguration : IEntityTypeConfiguration<SubmissionReview>
{
    public void Configure(EntityTypeBuilder<SubmissionReview> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Feedback)
            .HasMaxLength(5000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Submission)
            .WithOne(s => s.Review)
            .HasForeignKey<SubmissionReview>(x => x.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubmissionId).IsUnique();
    }
}
