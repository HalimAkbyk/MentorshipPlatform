using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class BookingQuestionResponseConfiguration : IEntityTypeConfiguration<BookingQuestionResponse>
{
    public void Configure(EntityTypeBuilder<BookingQuestionResponse> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AnswerText)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BookingId);
        builder.HasIndex(x => new { x.BookingId, x.QuestionId }).IsUnique();
    }
}
