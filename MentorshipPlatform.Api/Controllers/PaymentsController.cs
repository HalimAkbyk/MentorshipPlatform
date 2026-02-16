using MediatR;
using MentorshipPlatform.Api.Middleware;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Payments.Commands.CreateOrder;
using MentorshipPlatform.Application.Payments.Commands.ProcessPaymentWebhook;
using MentorshipPlatform.Application.Payments.Queries.GetStudentPaymentHistory;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;

    public PaymentsController(IMediator mediator,
        ILogger<ExceptionHandlingMiddleware> logger,
        IConfiguration configuration,
        IApplicationDbContext context)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    [HttpGet("my-orders")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var result = await _mediator.Send(new GetStudentPaymentHistoryQuery(page, pageSize, status));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpPost("orders")]
    [Authorize]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }
    // Iyzico callback - POST (checkout form completion) ve GET (3D Secure redirect) destekler
    [HttpPost("callback/iyzico")]
    [AllowAnonymous]
    public async Task<IActionResult> IyzicoCallbackPost([FromForm] string token)
    {
        return await ProcessIyzicoCallback(token);
    }

    [HttpGet("callback/iyzico")]
    [AllowAnonymous]
    public async Task<IActionResult> IyzicoCallbackGet([FromQuery] string? token)
    {
        return await ProcessIyzicoCallback(token);
    }

    private string GetFrontendUrl()
    {
        // Koyeb'de Frontend__BaseUrl, local'de FrontendUrl kullanılır
        return _configuration["Frontend:BaseUrl"]
            ?? _configuration["FrontendUrl"]
            ?? "https://mentorship-platform-react.vercel.app";
    }

    private async Task<IActionResult> ProcessIyzicoCallback(string? token)
    {
        var frontendUrl = GetFrontendUrl();
        _logger.LogInformation("📥 Iyzico callback - Token: {Token}, Method: {Method}, FrontendUrl: {FrontendUrl}",
            token, Request.Method, frontendUrl);

        if (string.IsNullOrEmpty(token))
        {
            return Redirect($"{frontendUrl}/api/payment/failed");
        }

        try
        {
            var command = new ProcessPaymentWebhookCommand(token);
            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("❌ Payment verification failed for token: {Token}", token);
                return Redirect($"{frontendUrl}/payment/failed");
            }

            _logger.LogInformation("✅ Payment successful, redirecting to frontend");
            return Redirect($"{frontendUrl}/api/payment/success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Callback error");
            return Redirect($"{frontendUrl}/api/payment/failed");
        }
    }
    /// <summary>
    /// Frontend proxy üzerinden gelen ödeme doğrulama isteği.
    /// Iyzico callback → Vercel (frontend route.ts) → bu endpoint
    /// </summary>
    [HttpPost("verify-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyCallback([FromBody] VerifyCallbackRequest request)
    {
        _logger.LogInformation("📥 Verify callback from frontend - Token: {Token}", request.Token);

        if (string.IsNullOrEmpty(request.Token))
            return BadRequest(new { isSuccess = false, error = "Token is required" });

        try
        {
            var command = new ProcessPaymentWebhookCommand(request.Token);
            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("❌ Payment verification failed for token: {Token}", request.Token);
                return Ok(new { isSuccess = false, error = "Payment verification failed" });
            }

            // Look up order type for frontend redirect
            var order = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.CheckoutToken == request.Token);

            var orderType = order?.Type.ToString() ?? "Booking";

            // If it's a course order, get the courseId from enrollment
            string? courseId = null;
            if (order?.Type == OrderType.Course)
            {
                var enrollment = await _context.CourseEnrollments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == order.ResourceId);
                courseId = enrollment?.CourseId.ToString();
            }

            _logger.LogInformation("✅ Payment verified - Type: {Type}, CourseId: {CourseId}", orderType, courseId);
            return Ok(new { isSuccess = true, orderType, courseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Verify callback error");
            return Ok(new { isSuccess = false, error = ex.Message });
        }
    }

    [HttpPost("webhook/iyzico")]
    [AllowAnonymous]
    public async Task<IActionResult> IyzicoWebhook([FromForm] IyzicoWebhookDto webhook)
    {
        var command = new ProcessPaymentWebhookCommand(
            webhook.Token);

        var result = await _mediator.Send(command);
        
        if (!result.IsSuccess)
            return BadRequest();

        return Ok();
    }
}
public record IyzicoWebhookDto(string Token);
public record VerifyCallbackRequest(string Token);
