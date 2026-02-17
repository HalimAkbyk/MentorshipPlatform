using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.DiscountType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.DiscountValue)
            .HasPrecision(18, 2);

        builder.Property(c => c.MaxDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.MinOrderAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.CreatedByRole)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.TargetType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.TargetProductType)
            .HasMaxLength(50);

        builder.HasIndex(c => c.CreatedByUserId);
        builder.HasIndex(c => c.MentorUserId);
        builder.HasIndex(c => c.IsActive);
    }
}
