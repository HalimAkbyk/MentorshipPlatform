using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class BookingQuestionConfiguration : IEntityTypeConfiguration<BookingQuestion>
{
    public void Configure(EntityTypeBuilder<BookingQuestion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuestionText)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(x => x.OfferingId);
        builder.HasIndex(x => new { x.OfferingId, x.SortOrder });
    }
}
