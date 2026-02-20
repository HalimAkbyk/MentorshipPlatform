namespace MentorshipPlatform.Domain.Events;

public record PaymentVerifyResult(
    bool IsSuccess,
    string? OrderId,
    string? ProviderPaymentId,
    string? ProviderTransactionId,
    string? Price,
    string? PaidPrice,
    string? ErrorMessage);