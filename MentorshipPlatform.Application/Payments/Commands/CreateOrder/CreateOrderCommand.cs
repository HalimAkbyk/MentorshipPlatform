using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Payments.Commands.CreateOrder;

public record CreateOrderResponse(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string? CheckoutFormContent,  // ✅ YENİ - HTML content from Iyzico
    string? PaymentPageUrl,       // ✅ YENİ - Redirect URL (fallback)
    string? Token                 // ✅ YENİ - Checkout form token
);
public record CreateOrderCommand(
    string Type,
    Guid ResourceId,
    string? BuyerName,      // ✅ YENİ
    string? BuyerSurname,   // ✅ YENİ
    string? BuyerPhone      // ✅ YENİ
) : IRequest<Result<CreateOrderResponse>>;

public class CreateOrderCommandHandler 
    : IRequestHandler<CreateOrderCommand, Result<CreateOrderResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IApplicationDbContext context,
        IPaymentService paymentService,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _context = context;
        _paymentService = paymentService;
        _currentUser = currentUser;
        _history = history;
        _logger = logger;
    }

    public async Task<Result<CreateOrderResponse>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            Guid? buyerUserId = _currentUser.UserId;
            if (buyerUserId == Guid.Empty)
            {
                return Result<CreateOrderResponse>.Failure("Unauthorized");
            }

            // Get user info
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == buyerUserId, cancellationToken);

            if (user == null)
            {
                return Result<CreateOrderResponse>.Failure("User not found");
            }

            // Parse order type
            if (!Enum.TryParse<OrderType>(request.Type, true, out var orderType))
            {
                return Result<CreateOrderResponse>.Failure("Invalid order type");
            }

            // Get resource and calculate amount
            decimal amount = 0;
            string currency = "TRY";

            if (orderType == OrderType.Booking)
            {
                var booking = await _context.Bookings
                    .Include(b => b.Offering)
                    .FirstOrDefaultAsync(b => b.Id == request.ResourceId, cancellationToken);

                if (booking == null)
                {
                    return Result<CreateOrderResponse>.Failure("Booking not found");
                }

                if (booking.Offering == null)
                {
                    return Result<CreateOrderResponse>.Failure("Offering not found");
                }

                // Fiyatı booking süresine göre oranla
                // Offering: 60dk = 300₺ ise, 70dk booking = (300/60)*70 = 350₺
                var offeringDuration = booking.Offering.DurationMinDefault;
                var bookingDuration = booking.DurationMin;

                if (offeringDuration > 0 && bookingDuration > 0 && bookingDuration != offeringDuration)
                {
                    amount = (booking.Offering.PriceAmount / offeringDuration) * bookingDuration;
                }
                else
                {
                    amount = booking.Offering.PriceAmount;
                }

                currency = booking.Offering.Currency;
            }
            else if (orderType == OrderType.GroupClass)
            {
                // GroupClass logic...
                return Result<CreateOrderResponse>.Failure("GroupClass not implemented yet");
            }
            else if (orderType == OrderType.Course)
            {
                var enrollment = await _context.CourseEnrollments
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.Id == request.ResourceId, cancellationToken);

                if (enrollment == null)
                {
                    return Result<CreateOrderResponse>.Failure("Course enrollment not found");
                }

                if (enrollment.Course == null)
                {
                    return Result<CreateOrderResponse>.Failure("Course not found");
                }

                amount = enrollment.Course.Price;
                currency = enrollment.Course.Currency;
            }

            // Platform fee (7%)
            var platformFee = amount * 0.07m;
            var totalAmount = amount + platformFee;

            // Create order
            var order = Order.Create(
                buyerUserId,
                orderType,
                request.ResourceId,
                totalAmount,
                currency);

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "✅ Order created - OrderId: {OrderId}, Amount: {Amount}", 
                order.Id, 
                totalAmount);

            // ✅ Initialize checkout form with buyer details
            var paymentResult = await _paymentService.InitializeCheckoutFormAsync(
                order.Id,
                totalAmount,
                currency,
                user.Email,
                request.BuyerName ?? user.DisplayName?.Split(' ').FirstOrDefault() ?? "User",
                request.BuyerSurname ?? user.DisplayName?.Split(' ').Skip(1).FirstOrDefault() ?? "User",
                request.BuyerPhone ?? user.Phone ?? "5555555555",
                cancellationToken);

            if (!paymentResult.IsSuccess)
            {
                _logger.LogError(
                    "❌ Checkout form initialization failed: {Error}", 
                    paymentResult.ErrorMessage);

                return Result<CreateOrderResponse>.Failure(
                    paymentResult.ErrorMessage ?? "Payment initialization failed");
            }

            // Save checkout token for reconciliation
            if (!string.IsNullOrEmpty(paymentResult.Token))
            {
                order.SetCheckoutToken(paymentResult.Token);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "✅ Checkout form initialized - Token: {Token}",
                paymentResult.Token);

            await _history.LogAsync("Order", order.Id, "Created",
                null, "Pending",
                $"Sipariş oluşturuldu. Tutar: {totalAmount} {currency}, Tip: {orderType}",
                buyerUserId, "Student", ct: cancellationToken);

            return Result<CreateOrderResponse>.Success(
                new CreateOrderResponse(
                    order.Id,
                    totalAmount,
                    currency,
                    paymentResult.CheckoutFormContent,
                    paymentResult.PaymentPageUrl,
                    paymentResult.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in CreateOrderCommandHandler");
            return Result<CreateOrderResponse>.Failure($"Error creating order: {ex.Message}");
        }
    }
}