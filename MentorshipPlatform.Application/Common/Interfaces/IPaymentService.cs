using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IPaymentService
{
    Task<CheckoutFormInitResult> InitializeCheckoutFormAsync(
        Guid orderId,
        decimal amount,
        string currency,
        string buyerEmail,
        string buyerName,
        string buyerSurname,
        string buyerPhone,
        CancellationToken cancellationToken = default);

    Task<PaymentVerifyResult> VerifyPaymentAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<RefundResult> RefundPaymentAsync(
        string providerPaymentId,
        decimal amount,
        CancellationToken cancellationToken = default);
}