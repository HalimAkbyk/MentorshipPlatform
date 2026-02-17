namespace MentorshipPlatform.Domain.Enums;

public enum OrderStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    PartiallyRefunded,
    Chargeback,
    Abandoned
}