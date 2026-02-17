using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Domain.Entities;

public class Order : BaseEntity
{
    public Guid BuyerUserId { get; private set; }
    public OrderType Type { get; private set; }
    public Guid ResourceId { get; private set; }
    public decimal AmountTotal { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public OrderStatus Status { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public string? PaymentProvider { get; private set; }
    public string? ProviderPaymentId { get; private set; }
    public string? CheckoutToken { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? CouponCode { get; private set; }
    public string? CouponCreatedByRole { get; private set; } // "Admin" or "Mentor" — determines who bears the discount cost

    private Order() { }

    public static Order Create(
        Guid? buyerUserId,
        OrderType type,
        Guid resourceId,
        decimal amount,
        string currency)
    {
        return new Order
        {
            BuyerUserId = buyerUserId ?? Guid.NewGuid(),
            Type = type,
            ResourceId = resourceId,
            AmountTotal = amount,
            Status = OrderStatus.Pending,
            Currency = currency,
        };
    }

    public void MarkAsPaid(string provider, string providerPaymentId)
    {
        Status = OrderStatus.Paid;
        PaymentProvider = provider;
        ProviderPaymentId = providerPaymentId;
        AddDomainEvent(new OrderPaidEvent(Id, BuyerUserId, Type, ResourceId, AmountTotal));
    }

    public void SetCheckoutToken(string token) => CheckoutToken = token;
    public void MarkAsFailed() => Status = OrderStatus.Failed;
    public void MarkAsAbandoned() => Status = OrderStatus.Abandoned;

    public void MarkAsRefunded()
    {
        RefundedAmount = AmountTotal;
        Status = OrderStatus.Refunded;
    }

    public void MarkAsPartiallyRefunded(decimal amount)
    {
        RefundedAmount += amount;
        Status = RefundedAmount >= AmountTotal
            ? OrderStatus.Refunded
            : OrderStatus.PartiallyRefunded;
    }

    public void ApplyCoupon(string couponCode, decimal discountAmount, string? couponCreatedByRole = null)
    {
        CouponCode = couponCode;
        DiscountAmount = discountAmount;
        CouponCreatedByRole = couponCreatedByRole;
        UpdatedAt = DateTime.UtcNow;
    }
}
