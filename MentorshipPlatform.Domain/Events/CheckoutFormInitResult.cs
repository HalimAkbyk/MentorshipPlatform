namespace MentorshipPlatform.Domain.Events;

public record CheckoutFormInitResult(
    bool IsSuccess,
    string? CheckoutFormContent,  // HTML content
    string? PaymentPageUrl,       // Redirect URL
    string? Token,                // Checkout form token
    long? TokenExpireTime,
    string? ErrorMessage);