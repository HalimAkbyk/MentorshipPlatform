namespace MentorshipPlatform.Domain.Events;

public record RefundResult(bool Success, string PaymentId,string? ErrorMessage);