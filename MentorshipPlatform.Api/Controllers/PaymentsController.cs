using MediatR;
using MentorshipPlatform.Api.Middleware;
using MentorshipPlatform.Application.Payments.Commands.CreateOrder;
using MentorshipPlatform.Application.Payments.Commands.ProcessPaymentWebhook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IConfiguration _configuration; // ✅ ekle

    public PaymentsController(IMediator mediator,
        ILogger<ExceptionHandlingMiddleware> logger,
        IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration; // ✅ assign et
        _logger = logger;
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

            _logger.LogInformation("✅ Payment verified successfully via frontend proxy");
            return Ok(new { isSuccess = true });
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
