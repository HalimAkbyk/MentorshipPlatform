using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        builder.ToTable("CreditTransactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.RelatedEntityType).HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasOne(e => e.StudentCredit)
            .WithMany()
            .HasForeignKey(e => e.StudentCreditId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.StudentCreditId);
        builder.HasIndex(e => e.InstructorId);
    }
}
