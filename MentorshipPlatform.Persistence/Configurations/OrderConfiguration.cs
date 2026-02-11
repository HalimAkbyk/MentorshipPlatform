using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.AmountTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.Currency)
            .HasMaxLength(3);

        builder.Property(o => o.PaymentProvider)
            .HasMaxLength(50);

        builder.Property(o => o.ProviderPaymentId)
            .HasMaxLength(255);

        builder.HasIndex(o => o.BuyerUserId);
        builder.HasIndex(o => o.ProviderPaymentId);
        builder.HasIndex(o => new { o.Type, o.ResourceId });
    }
}