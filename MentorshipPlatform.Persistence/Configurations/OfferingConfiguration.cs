using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class OfferingConfiguration : IEntityTypeConfiguration<Offering>
{
    public void Configure(EntityTypeBuilder<Offering> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("TRY");

        // Yeni alanlar
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Subtitle).HasMaxLength(150);
        builder.Property(x => x.DetailedDescription).HasColumnType("text");
        builder.Property(x => x.SessionType).HasMaxLength(50);
        builder.Property(x => x.MaxBookingDaysAhead).HasDefaultValue(60);
        builder.Property(x => x.MinNoticeHours).HasDefaultValue(2);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(500);

        // Navigation: Offering -> BookingQuestions
        builder.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(q => q.OfferingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MentorUserId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.MentorUserId, x.SortOrder });
    }
}
