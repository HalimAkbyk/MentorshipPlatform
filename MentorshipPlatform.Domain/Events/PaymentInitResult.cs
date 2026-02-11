namespace MentorshipPlatform.Domain.Events;

public record PaymentInitResult(
    bool Success,
    string? PaymentUrl,
    string? PaymentToken,
    string? ErrorMessage);