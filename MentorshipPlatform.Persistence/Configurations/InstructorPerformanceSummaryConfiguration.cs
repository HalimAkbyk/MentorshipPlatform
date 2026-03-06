using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class InstructorPerformanceSummaryConfiguration : IEntityTypeConfiguration<InstructorPerformanceSummary>
{
    public void Configure(EntityTypeBuilder<InstructorPerformanceSummary> builder)
    {
        builder.ToTable("InstructorPerformanceSummaries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PeriodType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.TotalDirectRevenue).HasPrecision(18, 2);
        builder.Property(e => e.TotalCreditRevenue).HasPrecision(18, 2);
        builder.Property(e => e.PrivateLessonDemandRate).HasPrecision(5, 2);
        builder.Property(e => e.GroupLessonFillRate).HasPrecision(5, 2);

        builder.HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.InstructorId, e.PeriodType, e.PeriodStart }).IsUnique();
    }
}
