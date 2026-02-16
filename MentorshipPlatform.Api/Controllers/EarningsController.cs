using MediatR;
using MentorshipPlatform.Application.Earnings.Queries.GetMentorEarningsSummary;
using MentorshipPlatform.Application.Earnings.Queries.GetMentorTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/earnings")]
[Authorize(Policy = "RequireMentorRole")]
public class EarningsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EarningsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(MentorEarningsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _mediator.Send(new GetMentorEarningsSummaryQuery());
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpGet("transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _mediator.Send(new GetMentorTransactionsQuery(page, pageSize, type, from, to));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }
}
