using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class ClassEnrollmentConfiguration : IEntityTypeConfiguration<ClassEnrollment>
{
    public void Configure(EntityTypeBuilder<ClassEnrollment> builder)
    {
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
        
        builder.HasIndex(x => new { ClassId = x.ClassId, x.StudentUserId })
            .IsUnique();

        // İsteğe bağlı: FK delete behavior senin manuel migration’daki gibi Restrict olsun
        builder.HasOne(x => x.Class)
            .WithMany() // veya .WithMany(c => c.Enrollments)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StudentUser)
            .WithMany() // veya .WithMany(u => u.ClassEnrollments)
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}