using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class CouponUsage : BaseEntity
{
    public Guid CouponId { get; private set; }
    public Coupon Coupon { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal DiscountApplied { get; private set; }

    private CouponUsage() { }

    public static CouponUsage Create(Guid couponId, Guid userId, Guid orderId, decimal discountApplied)
    {
        return new CouponUsage
        {
            CouponId = couponId,
            UserId = userId,
            OrderId = orderId,
            DiscountApplied = discountApplied
        };
    }
}
