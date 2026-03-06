using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class StudentCredit : BaseEntity
{
    public Guid StudentId { get; private set; }
    public Guid PackagePurchaseId { get; private set; }
    public CreditType CreditType { get; private set; }
    public int TotalCredits { get; private set; }
    public int UsedCredits { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    public User Student { get; private set; } = null!;
    public PackagePurchase PackagePurchase { get; private set; } = null!;

    public int RemainingCredits => TotalCredits - UsedCredits;

    private StudentCredit() { }

    public static StudentCredit Create(
        Guid studentId,
        Guid packagePurchaseId,
        CreditType creditType,
        int totalCredits,
        DateTime? expiresAt = null)
    {
        return new StudentCredit
        {
            StudentId = studentId,
            PackagePurchaseId = packagePurchaseId,
            CreditType = creditType,
            TotalCredits = totalCredits,
            UsedCredits = 0,
            ExpiresAt = expiresAt
        };
    }

    public bool HasAvailableCredits(int amount = 1)
    {
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow)
            return false;
        return RemainingCredits >= amount;
    }

    public void UseCredits(int amount)
    {
        if (!HasAvailableCredits(amount))
            throw new InvalidOperationException("Insufficient credits");
        UsedCredits += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RefundCredits(int amount)
    {
        UsedCredits = Math.Max(0, UsedCredits - amount);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        UsedCredits = TotalCredits;
        UpdatedAt = DateTime.UtcNow;
    }
}
