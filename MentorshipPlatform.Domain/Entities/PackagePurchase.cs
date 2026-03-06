using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class PackagePurchase : BaseEntity
{
    public Guid StudentId { get; private set; }
    public Guid PackageId { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public Guid OrderId { get; private set; }
    public DateTime PurchasedAt { get; private set; }
    public PackagePurchaseStatus Status { get; private set; }

    public User Student { get; private set; } = null!;
    public Package Package { get; private set; } = null!;
    public Order Order { get; private set; } = null!;

    private PackagePurchase() { }

    public static PackagePurchase Create(
        Guid studentId,
        Guid packageId,
        decimal purchasePrice,
        Guid orderId)
    {
        return new PackagePurchase
        {
            StudentId = studentId,
            PackageId = packageId,
            PurchasePrice = purchasePrice,
            OrderId = orderId,
            PurchasedAt = DateTime.UtcNow,
            Status = PackagePurchaseStatus.Completed
        };
    }

    public void MarkRefunded() => Status = PackagePurchaseStatus.Refunded;
    public void MarkPartial() => Status = PackagePurchaseStatus.Partial;
}
