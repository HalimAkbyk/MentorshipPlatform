using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.CancellationReason)
            .HasMaxLength(500);

        builder.HasOne(b => b.Student)
            .WithMany()
            .HasForeignKey(b => b.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Mentor)
            .WithMany()
            .HasForeignKey(b => b.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Offering)
            .WithMany()
            .HasForeignKey(b => b.OfferingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.StudentUserId);
        builder.HasIndex(b => b.MentorUserId);
        builder.HasIndex(b => b.StartAt);
        builder.HasIndex(b => b.Status);
    }
}