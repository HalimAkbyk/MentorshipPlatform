using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class StudentCreditConfiguration : IEntityTypeConfiguration<StudentCredit>
{
    public void Configure(EntityTypeBuilder<StudentCredit> builder)
    {
        builder.ToTable("StudentCredits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreditType).HasConversion<string>().HasMaxLength(30);
        builder.Ignore(e => e.RemainingCredits);

        builder.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PackagePurchase)
            .WithMany()
            .HasForeignKey(e => e.PackagePurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.StudentId, e.CreditType });
    }
}
