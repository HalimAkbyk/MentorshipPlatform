using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class LedgerEntry : BaseEntity
{
    public LedgerAccountType AccountType { get; private set; }
    public Guid? AccountOwnerUserId { get; private set; }
    public LedgerDirection Direction { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public string ReferenceType { get; private set; } = string.Empty;
    public Guid ReferenceId { get; private set; }

    private LedgerEntry() { }

    public static LedgerEntry Create(
        LedgerAccountType accountType,
        LedgerDirection direction,
        decimal amount,
        string referenceType,
        Guid referenceId,
        Guid? accountOwnerUserId = null)
    {
        return new LedgerEntry
        {
            AccountType = accountType,
            AccountOwnerUserId = accountOwnerUserId,
            Direction = direction,
            Amount = amount,
            ReferenceType = referenceType,
            ReferenceId = referenceId
        };
    }
}