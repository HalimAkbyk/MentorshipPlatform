using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuestionText)
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(x => x.QuestionType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.OptionsJson)
            .HasColumnType("text");

        builder.Property(x => x.CorrectAnswer)
            .HasMaxLength(2000);

        builder.Property(x => x.Explanation)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.ExamId);
    }
}
