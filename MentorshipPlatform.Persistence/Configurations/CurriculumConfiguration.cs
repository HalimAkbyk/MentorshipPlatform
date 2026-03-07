using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
{
    public void Configure(EntityTypeBuilder<Curriculum> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Subject)
            .HasMaxLength(100);

        builder.Property(x => x.Level)
            .HasMaxLength(100);

        builder.Property(x => x.CoverImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.IsTemplate)
            .HasDefaultValue(false);

        builder.Property(x => x.TemplateName)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MentorUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsTemplate);
    }
}

public class CurriculumWeekConfiguration : IEntityTypeConfiguration<CurriculumWeek>
{
    public void Configure(EntityTypeBuilder<CurriculumWeek> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.HasOne(x => x.Curriculum)
            .WithMany(x => x.Weeks)
            .HasForeignKey(x => x.CurriculumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CurriculumId);
    }
}

public class CurriculumTopicConfiguration : IEntityTypeConfiguration<CurriculumTopic>
{
    public void Configure(EntityTypeBuilder<CurriculumTopic> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.ObjectiveText)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Week)
            .WithMany(x => x.Topics)
            .HasForeignKey(x => x.CurriculumWeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CurriculumWeekId);
    }
}

public class CurriculumTopicMaterialConfiguration : IEntityTypeConfiguration<CurriculumTopicMaterial>
{
    public void Configure(EntityTypeBuilder<CurriculumTopicMaterial> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaterialRole)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(x => x.Topic)
            .WithMany(x => x.Materials)
            .HasForeignKey(x => x.CurriculumTopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LibraryItem)
            .WithMany()
            .HasForeignKey(x => x.LibraryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CurriculumTopicId);
        builder.HasIndex(x => x.LibraryItemId);
    }
}

public class StudentCurriculumEnrollmentConfiguration : IEntityTypeConfiguration<StudentCurriculumEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentCurriculumEnrollment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CompletionPercentage)
            .HasPrecision(5, 2);

        builder.HasOne(x => x.Curriculum)
            .WithMany()
            .HasForeignKey(x => x.CurriculumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CurriculumId);
        builder.HasIndex(x => x.StudentUserId);
        builder.HasIndex(x => x.MentorUserId);
    }
}

public class TopicProgressConfiguration : IEntityTypeConfiguration<TopicProgress>
{
    public void Configure(EntityTypeBuilder<TopicProgress> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.MentorNote)
            .HasMaxLength(2000);

        builder.HasOne(x => x.Enrollment)
            .WithMany(x => x.TopicProgresses)
            .HasForeignKey(x => x.StudentCurriculumEnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.CurriculumTopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StudentCurriculumEnrollmentId);
        builder.HasIndex(x => x.CurriculumTopicId);
    }
}
