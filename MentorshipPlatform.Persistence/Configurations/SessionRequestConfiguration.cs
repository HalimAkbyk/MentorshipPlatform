using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class SessionRequestConfiguration : IEntityTypeConfiguration<SessionRequest>
{
    public void Configure(EntityTypeBuilder<SessionRequest> builder)
    {
        builder.HasKey(sr => sr.Id);

        builder.Property(sr => sr.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(sr => sr.StudentNote)
            .HasMaxLength(500);

        builder.Property(sr => sr.ReviewerRole)
            .HasMaxLength(20);

        builder.Property(sr => sr.RejectionReason)
            .HasMaxLength(500);

        builder.HasOne(sr => sr.Student)
            .WithMany()
            .HasForeignKey(sr => sr.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.Mentor)
            .WithMany()
            .HasForeignKey(sr => sr.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.Offering)
            .WithMany()
            .HasForeignKey(sr => sr.OfferingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.Booking)
            .WithMany()
            .HasForeignKey(sr => sr.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(sr => sr.StudentUserId);
        builder.HasIndex(sr => sr.MentorUserId);
        builder.HasIndex(sr => sr.Status);
    }
}
