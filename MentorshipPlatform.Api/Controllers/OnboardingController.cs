using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Onboarding.Commands.SaveStudentOnboarding;
using MentorshipPlatform.Application.Onboarding.Commands.SaveMentorOnboarding;
using MentorshipPlatform.Application.Onboarding.Queries.GetStudentOnboarding;
using MentorshipPlatform.Application.Onboarding.Queries.GetMentorOnboarding;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAdminNotificationService _adminNotifications;

    public OnboardingController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAdminNotificationService adminNotifications)
    {
        _mediator = mediator;
        _db = db;
        _currentUser = currentUser;
        _adminNotifications = adminNotifications;
    }

    // ─── Student Onboarding ───

    [HttpGet("student")]
    public async Task<IActionResult> GetStudentOnboarding()
    {
        var result = await _mediator.Send(new GetStudentOnboardingQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpPut("student")]
    public async Task<IActionResult> SaveStudentOnboarding([FromBody] SaveStudentOnboardingCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // ─── Mentor Onboarding ───

    [HttpGet("mentor")]
    public async Task<IActionResult> GetMentorOnboarding()
    {
        var result = await _mediator.Send(new GetMentorOnboardingQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpPut("mentor")]
    public async Task<IActionResult> SaveMentorOnboarding([FromBody] SaveMentorOnboardingCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // ─── Mentor Review Request ───

    [HttpGet("mentor/review-status")]
    public async Task<IActionResult> GetMentorReviewStatus()
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var userId = _currentUser.UserId.Value;

        var mentorProfile = await _db.MentorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId);

        return Ok(new
        {
            hasPendingReview = mentorProfile?.HasPendingReviewRequest ?? false
        });
    }

    [HttpGet("mentor/review-notes")]
    public async Task<IActionResult> GetMentorReviewNotes()
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var userId = _currentUser.UserId.Value;

        var notes = await _db.MentorReviewNotes
            .AsNoTracking()
            .Where(n => n.MentorUserId == userId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.SenderRole,
                n.Message,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(notes);
    }

    public record ReviewResponseRequest(string Message);

    [HttpPost("mentor/review-response")]
    public async Task<IActionResult> RespondToReviewRequest([FromBody] ReviewResponseRequest request)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { errors = new[] { "Mesaj bos olamaz" } });

        var userId = _currentUser.UserId.Value;

        // Save the note
        var note = MentorReviewNote.Create(userId, userId, "Mentor", request.Message.Trim());
        _db.MentorReviewNotes.Add(note);

        // Notify admin
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        var displayName = user?.DisplayName ?? "Egitmen";

        await _adminNotifications.CreateOrUpdateGroupedAsync(
            "MentorProfileUpdate",
            $"mentor-profile-{userId}",
            count => ("Egitmen yanit verdi", $"{displayName}: \"{request.Message.Trim()}\""),
            "MentorApproval",
            userId);

        // Clear pending flag if exists
        var mentorProfile = await _db.MentorProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (mentorProfile is { HasPendingReviewRequest: true })
            mentorProfile.ClearReviewRequest();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            note.Id,
            note.SenderRole,
            note.Message,
            note.CreatedAt
        });
    }
}
