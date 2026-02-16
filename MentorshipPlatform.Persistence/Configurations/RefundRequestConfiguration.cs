using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.Reason)
            .HasMaxLength(1000);

        builder.Property(r => r.AdminNotes)
            .HasMaxLength(1000);

        builder.Property(r => r.RequestedAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.ApprovedAmount)
            .HasPrecision(18, 2);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.RequestedByUserId);
        builder.HasIndex(r => r.Status);
    }
}
