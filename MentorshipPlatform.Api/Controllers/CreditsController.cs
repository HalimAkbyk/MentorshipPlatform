using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MentorshipPlatform.Application.Credits.Queries.GetMyCredits;
using MentorshipPlatform.Application.Credits.Queries.GetMyCreditTransactions;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/credits")]
[Authorize]
public class CreditsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CreditsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get current user's credits summary (grouped by type, active only)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CreditSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCredits()
    {
        var result = await _mediator.Send(new GetMyCreditsQuery());
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Get current user's credit transactions (paginated)
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(CreditTransactionPagedResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? creditType = null)
    {
        var result = await _mediator.Send(new GetMyCreditTransactionsQuery(page, pageSize, creditType));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }
}
