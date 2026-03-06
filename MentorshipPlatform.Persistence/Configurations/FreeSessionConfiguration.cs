using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class FreeSessionConfiguration : IEntityTypeConfiguration<FreeSession>
{
    public void Configure(EntityTypeBuilder<FreeSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoomName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreditTransaction)
            .WithMany()
            .HasForeignKey(x => x.CreditTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MentorUserId);
        builder.HasIndex(x => x.StudentUserId);
        builder.HasIndex(x => x.Status);
    }
}
