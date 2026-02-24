using MediatR;
using MentorshipPlatform.Application.Bookings.Commands.CancelBooking;
using MentorshipPlatform.Application.Bookings.Commands.CreateBooking;
using MentorshipPlatform.Application.Bookings.Commands.CompleteBooking;
using MentorshipPlatform.Application.Bookings.Commands.DisputeBooking;
using MentorshipPlatform.Application.Bookings.Commands.RescheduleBooking;
using MentorshipPlatform.Application.Bookings.Commands.ApproveReschedule;
using MentorshipPlatform.Application.Bookings.Commands.RejectReschedule;
using MentorshipPlatform.Application.Bookings.Queries.GetMyBookings;
using MentorshipPlatform.Application.Bookings.Queries.GetBookingById;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Policy = "RequireStudentRole")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingCommand command)
    {
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { bookingId = result.Data });
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(PaginatedList<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] BookingStatus? status,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15)
    {
        var result = await _mediator.Send(new GetMyBookingsQuery(status, role, page, pageSize));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBookingById(Guid id)
    {
        var result = await _mediator.Send(new GetBookingByIdQuery(id));

        if (!result.IsSuccess)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelBookingCommand command)
    {
        // Route id her zaman baskın olsun
        var fixedCommand = new CancelBookingCommand(id, command.Reason);

        var result = await _mediator.Send(fixedCommand);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok();
    }

    [HttpPost("{id}/complete")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> CompleteBooking(Guid id)
    {
        var result = await _mediator.Send(new CompleteBookingCommand(id));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok();
    }

    [HttpPost("{id}/dispute")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> DisputeBooking(Guid id, [FromBody] DisputeBookingCommand command)
    {
        var fixedCommand = new DisputeBookingCommand(id, command.Reason);
        var result = await _mediator.Send(fixedCommand);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok();
    }

    // ─── Reschedule Endpoints ───

    [HttpPost("{id}/reschedule")]
    public async Task<IActionResult> RescheduleBooking(Guid id, [FromBody] RescheduleBookingRequest body)
    {
        var result = await _mediator.Send(new RescheduleBookingCommand(id, body.NewStartAt));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok();
    }

    [HttpPost("{id}/reschedule/approve")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> ApproveReschedule(Guid id)
    {
        var result = await _mediator.Send(new ApproveRescheduleCommand(id));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok();
    }

    [HttpPost("{id}/reschedule/reject")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> RejectReschedule(Guid id)
    {
        var result = await _mediator.Send(new RejectRescheduleCommand(id));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok();
    }
}

public record RescheduleBookingRequest(DateTime NewStartAt);
