using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class ExamAnswerConfiguration : IEntityTypeConfiguration<ExamAnswer>
{
    public void Configure(EntityTypeBuilder<ExamAnswer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AnswerText)
            .HasColumnType("text");

        builder.Property(x => x.SelectedOptionsJson)
            .HasColumnType("text");

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AttemptId);
        builder.HasIndex(x => x.QuestionId);
    }
}
