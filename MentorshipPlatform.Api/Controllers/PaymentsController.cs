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
    [HttpPost("callback/iyzico")]
    [AllowAnonymous]
    public async Task<IActionResult> IyzicoCallback([FromForm] string token)
    {
        _logger.LogInformation("📥 Iyzico callback - Token: {Token}", token);

        if (string.IsNullOrEmpty(token))
            return BadRequest("Token is required");

        try
        {
            var command = new ProcessPaymentWebhookCommand(token);
            var result = await _mediator.Send(command);
        
            if (!result.IsSuccess)
            {
                var frontendUrl = _configuration["FrontendUrl"];
                return Redirect($"{frontendUrl}/payment/failed");
            }

            var frontendSuccessUrl = _configuration["FrontendUrl"];
            return Redirect($"{frontendSuccessUrl}/api/payment/success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Callback error");
            var frontendUrl = _configuration["FrontendUrl"];
            return Redirect($"{frontendUrl}/api/payment/failed");
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
