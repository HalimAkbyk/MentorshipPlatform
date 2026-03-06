using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class CreditTransaction : BaseEntity
{
    public Guid StudentCreditId { get; private set; }
    public CreditTransactionType TransactionType { get; private set; }
    public int Amount { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public Guid? InstructorId { get; private set; }
    public string? Description { get; private set; }

    public StudentCredit StudentCredit { get; private set; } = null!;
    public User? Instructor { get; private set; }

    private CreditTransaction() { }

    public static CreditTransaction Create(
        Guid studentCreditId,
        CreditTransactionType transactionType,
        int amount,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? instructorId = null,
        string? description = null)
    {
        return new CreditTransaction
        {
            StudentCreditId = studentCreditId,
            TransactionType = transactionType,
            Amount = amount,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            InstructorId = instructorId,
            Description = description
        };
    }
}
