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

        if (mentorProfile is not { HasPendingReviewRequest: true })
            return Ok(new { hasPendingReview = false });

        // Get the latest admin message notification for this user
        var lastAdminMessage = await _db.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == "AdminMessage" && n.ReferenceType == "MentorApproval")
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Title, n.Message })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            hasPendingReview = true,
            adminTitle = lastAdminMessage?.Title,
            adminMessage = lastAdminMessage?.Message
        });
    }

    public record ReviewResponseRequest(string? Message);

    [HttpPost("mentor/review-response")]
    public async Task<IActionResult> RespondToReviewRequest([FromBody] ReviewResponseRequest request)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var userId = _currentUser.UserId.Value;

        var mentorProfile = await _db.MentorProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (mentorProfile is not { HasPendingReviewRequest: true })
            return BadRequest(new { errors = new[] { "Aktif duzenleme talebi yok" } });

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        var displayName = user?.DisplayName ?? "Egitmen";
        var responseMessage = string.IsNullOrWhiteSpace(request.Message)
            ? $"{displayName} istenen duzenlemeyi yapti ve tekrar incelemenizi bekliyor."
            : $"{displayName} duzenleme talebine cevap verdi: \"{request.Message}\"";

        await _adminNotifications.CreateOrUpdateGroupedAsync(
            "MentorProfileUpdate",
            $"mentor-profile-{userId}",
            count => ("Egitmen duzenleme talebi yaniti", responseMessage),
            "MentorApproval",
            userId);

        mentorProfile.ClearReviewRequest();
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
