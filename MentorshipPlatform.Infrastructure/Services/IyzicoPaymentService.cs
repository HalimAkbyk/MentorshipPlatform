using System.Globalization;
using Iyzipay.Model;
using Iyzipay.Request;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options = Iyzipay.Options;

namespace MentorshipPlatform.Infrastructure.Services;

public class IyzicoPaymentService : IPaymentService
{
    private readonly IyzicoOptions _options;
    private readonly ILogger<IyzicoPaymentService> _logger;
    private readonly Options _iyzicoOptions;

    public IyzicoPaymentService(
        IOptions<IyzicoOptions> options,
        ILogger<IyzicoPaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        _iyzicoOptions = new Options
        {
            ApiKey = _options.ApiKey,
            SecretKey = _options.SecretKey,
            BaseUrl = _options.BaseUrl
        };
    }

    public async Task<CheckoutFormInitResult> InitializeCheckoutFormAsync(
        Guid orderId,
        decimal amount,
        string currency,
        string buyerEmail,
        string buyerName,
        string buyerSurname,
        string buyerPhone,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CreateCheckoutFormInitializeRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = orderId.ToString(),
                Price = amount.ToString("F2", CultureInfo.InvariantCulture),
                PaidPrice = amount.ToString("F2", CultureInfo.InvariantCulture),
                Currency = Currency.TRY.ToString(),
                BasketId = orderId.ToString(),
                PaymentGroup = PaymentGroup.PRODUCT.ToString(),
                CallbackUrl = _options.CallbackUrl,
                EnabledInstallments = new List<int> { 1, 2, 3, 6, 9 }
            };

            // Buyer info
            request.Buyer = new Buyer
            {
                Id = "BY" + orderId.ToString("N")[..11],
                Name = buyerName,
                Surname = buyerSurname,
                Email = buyerEmail,
                GsmNumber = buyerPhone,
                IdentityNumber = "11111111111",
                RegistrationAddress = "Adres Bilgisi",
                City = "Istanbul",
                Country = "Turkey",
                Ip = "85.34.78.112"
            };

            // Billing Address
            request.BillingAddress = new Address
            {
                ContactName = $"{buyerName} {buyerSurname}",
                City = "Istanbul",
                Country = "Turkey",
                Description = "Fatura Adresi"
            };

            // Shipping Address
            request.ShippingAddress = new Address
            {
                ContactName = $"{buyerName} {buyerSurname}",
                City = "Istanbul",
                Country = "Turkey",
                Description = "Teslimat Adresi"
            };

            // Basket items
            var basketItems = new List<BasketItem>
            {
                new BasketItem
                {
                    Id = "BI" + orderId.ToString("N")[..11],
                    Name = "Mentorluk Hizmeti",
                    Category1 = "Eğitim",
                    ItemType = BasketItemType.VIRTUAL.ToString(),
                    Price = amount.ToString("F2", CultureInfo.InvariantCulture)
                }
            };
            request.BasketItems = basketItems;

            var checkoutForm = await Task.Run(() => 
                CheckoutFormInitialize.Create(request, _iyzicoOptions), 
                cancellationToken);

            if (checkoutForm.Status == "success")
            {
                _logger.LogInformation(
                    "✅ Checkout form initialized - Token: {Token}", 
                    checkoutForm.Token);

                return new CheckoutFormInitResult(
                    true,
                    checkoutForm.CheckoutFormContent,
                    checkoutForm.PaymentPageUrl,
                    checkoutForm.Token,
                    checkoutForm.TokenExpireTime,
                    null);
            }

            _logger.LogError(
                "❌ Iyzico checkout form init failed: {ErrorMessage}", 
                checkoutForm.ErrorMessage);

            return new CheckoutFormInitResult(
                false, 
                null, 
                null, 
                null, 
                null, 
                checkoutForm.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception in InitializeCheckoutFormAsync");
            return new CheckoutFormInitResult(
                false, 
                null, 
                null, 
                null, 
                null, 
                ex.Message);
        }
    }

    public async Task<PaymentVerifyResult> VerifyPaymentAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var request = new RetrieveCheckoutFormRequest { Token = token};
        var checkoutForm = CheckoutForm.Retrieve(request, _iyzicoOptions);

        if (checkoutForm.Status == "success" &&
            checkoutForm.PaymentStatus == "SUCCESS")
        {
            // Extract PaymentTransactionId from PaymentItems (required for refunds)
            string? transactionId = null;
            if (checkoutForm.PaymentItems != null && checkoutForm.PaymentItems.Count > 0)
            {
                transactionId = checkoutForm.PaymentItems[0].PaymentTransactionId;
                _logger.LogInformation(
                    "✅ PaymentTransactionId extracted: {TransactionId} (PaymentId: {PaymentId})",
                    transactionId, checkoutForm.PaymentId);
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ No PaymentItems found in checkout form. PaymentId: {PaymentId}",
                    checkoutForm.PaymentId);
            }

            return new PaymentVerifyResult(
                true,
                checkoutForm.BasketId,      // ✅ Order.Id
                checkoutForm.PaymentId?.ToString(),
                transactionId,
                checkoutForm.Price,
                checkoutForm.PaidPrice,
                null);
        }

        return new PaymentVerifyResult(
            false,
            checkoutForm.ConversationId,
            null,
            null,
            null,
            null,
            checkoutForm.ErrorMessage);
    }

    public async Task<RefundResult> RefundPaymentAsync(
        string providerPaymentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var transactionId = providerPaymentId;

            // If this looks like a PaymentId (not a PaymentTransactionId),
            // retrieve the payment to get the correct PaymentTransactionId.
            // PaymentTransactionIds are typically numeric, PaymentIds are longer strings.
            // Try refund first; if it fails with "ödeme kırılım kaydı bulunamadı",
            // fall back to retrieving the transaction ID from the payment.
            var request = new CreateRefundRequest
            {
                PaymentTransactionId = transactionId,
                Price = amount.ToString("F2", CultureInfo.InvariantCulture),
                Currency = Currency.TRY.ToString()
            };

            var refund = await Task.Run(() =>
                Refund.Create(request, _iyzicoOptions),
                cancellationToken);

            if (refund.Status == "success")
            {
                _logger.LogInformation("✅ Refund successful with TransactionId: {TransactionId}", transactionId);
                return new RefundResult(
                    true,
                    refund.PaymentId.ToString(),
                    null);
            }

            // If refund failed, try to retrieve the payment and get the correct PaymentTransactionId
            if (refund.ErrorMessage != null && refund.ErrorMessage.Contains("kırılım"))
            {
                _logger.LogWarning(
                    "⚠️ Refund failed with '{Error}'. Attempting to retrieve PaymentTransactionId via Payment.Retrieve for PaymentId: {PaymentId}",
                    refund.ErrorMessage, providerPaymentId);

                try
                {
                    var retrieveRequest = new RetrievePaymentRequest { PaymentId = providerPaymentId };
                    var payment = await Task.Run(() =>
                        Payment.Retrieve(retrieveRequest, _iyzicoOptions),
                        cancellationToken);

                    if (payment.Status == "success" && payment.PaymentItems?.Count > 0)
                    {
                        var correctTransactionId = payment.PaymentItems[0].PaymentTransactionId;
                        _logger.LogInformation(
                            "✅ Retrieved correct PaymentTransactionId: {TransactionId} (was using: {OldId})",
                            correctTransactionId, providerPaymentId);

                        // Retry refund with correct transaction ID
                        var retryRequest = new CreateRefundRequest
                        {
                            PaymentTransactionId = correctTransactionId,
                            Price = amount.ToString("F2", CultureInfo.InvariantCulture),
                            Currency = Currency.TRY.ToString()
                        };

                        var retryRefund = await Task.Run(() =>
                            Refund.Create(retryRequest, _iyzicoOptions),
                            cancellationToken);

                        if (retryRefund.Status == "success")
                        {
                            _logger.LogInformation("✅ Retry refund successful with correct TransactionId: {TransactionId}",
                                correctTransactionId);
                            return new RefundResult(
                                true,
                                retryRefund.PaymentId.ToString(),
                                null);
                        }

                        return new RefundResult(false, null, retryRefund.ErrorMessage);
                    }
                    else
                    {
                        _logger.LogError("❌ Payment.Retrieve failed or no PaymentItems: {Error}", payment.ErrorMessage);
                    }
                }
                catch (Exception retrieveEx)
                {
                    _logger.LogError(retrieveEx, "❌ Failed to retrieve payment for fallback refund");
                }
            }

            return new RefundResult(false, null, refund.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception in RefundPaymentAsync");
            return new RefundResult(false, null, ex.Message);
        }
    }
}

public class IyzicoOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}