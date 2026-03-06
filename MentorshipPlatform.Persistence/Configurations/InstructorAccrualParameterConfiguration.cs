using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class InstructorAccrualParameterConfiguration : IEntityTypeConfiguration<InstructorAccrualParameter>
{
    public void Configure(EntityTypeBuilder<InstructorAccrualParameter> builder)
    {
        builder.ToTable("InstructorAccrualParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PrivateLessonRate).HasPrecision(18, 2);
        builder.Property(e => e.GroupLessonRate).HasPrecision(18, 2);
        builder.Property(e => e.VideoContentRate).HasPrecision(18, 2);
        builder.Property(e => e.BonusPercentage).HasPrecision(5, 2);

        builder.HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.InstructorId, e.IsActive });
    }
}
