using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.HasKey(cu => cu.Id);

        builder.Property(cu => cu.DiscountApplied)
            .HasPrecision(18, 2);

        builder.HasOne(cu => cu.Coupon)
            .WithMany()
            .HasForeignKey(cu => cu.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cu => new { cu.CouponId, cu.UserId });
        builder.HasIndex(cu => cu.OrderId);
    }
}
