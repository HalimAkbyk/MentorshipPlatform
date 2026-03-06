using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class InstructorAccrualConfiguration : IEntityTypeConfiguration<InstructorAccrual>
{
    public void Configure(EntityTypeBuilder<InstructorAccrual> builder)
    {
        builder.ToTable("InstructorAccruals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.PrivateLessonUnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.GroupLessonUnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.VideoUnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.BonusAmount).HasPrecision(18, 2);
        builder.Property(e => e.BonusDescription).HasMaxLength(500);
        builder.Property(e => e.TotalAccrual).HasPrecision(18, 2);
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.InstructorId);
        builder.HasIndex(e => e.Status);
    }
}
