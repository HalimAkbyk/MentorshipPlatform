namespace MentorshipPlatform.Api.Controllers;

using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// DTOs
public class BlacklistEntryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBlacklistEntryRequest
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class ContentReviewItemDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;  // MentorProfile, Course, GroupClass
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[ApiController]
[Route("api/admin/moderation")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminModerationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdminModerationController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // -----------------------------
    // BLACKLIST
    // -----------------------------

    [HttpGet("blacklist")]
    public async Task<ActionResult<List<BlacklistEntryDto>>> GetBlacklist([FromQuery] string? type = null)
    {
        var query = _db.BlacklistEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(e => e.Type == type);
        }

        var items = await (
            from b in query
            join u in _db.Users.AsNoTracking() on b.CreatedByUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby b.CreatedAt descending
            select new BlacklistEntryDto
            {
                Id = b.Id,
                Type = b.Type,
                Value = b.Value,
                Reason = b.Reason,
                CreatedByName = u != null ? u.DisplayName : null,
                CreatedAt = b.CreatedAt
            }
        ).ToListAsync();

        return Ok(items);
    }

    [HttpPost("blacklist")]
    public async Task<IActionResult> CreateBlacklistEntry([FromBody] CreateBlacklistEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Value))
            return BadRequest(new { errors = new[] { "Type and Value are required." } });

        var allowedTypes = new[] { "Word", "IP", "Email" };
        if (!allowedTypes.Contains(request.Type))
            return BadRequest(new { errors = new[] { "Type must be one of: Word, IP, Email." } });

        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Unauthorized();

        var entry = BlacklistEntry.Create(request.Type, request.Value, request.Reason, userId.Value);
        _db.BlacklistEntries.Add(entry);
        await _db.SaveChangesAsync();

        return Ok(new BlacklistEntryDto
        {
            Id = entry.Id,
            Type = entry.Type,
            Value = entry.Value,
            Reason = entry.Reason,
            CreatedAt = entry.CreatedAt
        });
    }

    [HttpDelete("blacklist/{id:guid}")]
    public async Task<IActionResult> DeleteBlacklistEntry([FromRoute] Guid id)
    {
        var entry = await _db.BlacklistEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound();

        _db.BlacklistEntries.Remove(entry);
        await _db.SaveChangesAsync();

        return Ok();
    }

    // -----------------------------
    // CONTENT REVIEW
    // -----------------------------

    [HttpGet("content")]
    public async Task<ActionResult<List<ContentReviewItemDto>>> GetContentForReview()
    {
        // Recent MentorProfiles (last 50)
        var mentorProfiles = await (
            from mp in _db.MentorProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on mp.UserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby mp.CreatedAt descending
            select new ContentReviewItemDto
            {
                Id = mp.Id,
                EntityType = "MentorProfile",
                Title = mp.Headline ?? "No Headline",
                Description = mp.Bio,
                OwnerName = u != null ? u.DisplayName : "Unknown",
                CreatedAt = mp.CreatedAt
            }
        ).Take(50).ToListAsync();

        // Recent Courses (last 50)
        var courses = await (
            from c in _db.Courses.AsNoTracking()
            join u in _db.Users.AsNoTracking() on c.MentorUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby c.CreatedAt descending
            select new ContentReviewItemDto
            {
                Id = c.Id,
                EntityType = "Course",
                Title = c.Title,
                Description = c.Description,
                OwnerName = u != null ? u.DisplayName : "Unknown",
                CreatedAt = c.CreatedAt
            }
        ).Take(50).ToListAsync();

        // Recent GroupClasses (last 50)
        var groupClasses = await (
            from gc in _db.GroupClasses.AsNoTracking()
            join u in _db.Users.AsNoTracking() on gc.MentorUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby gc.CreatedAt descending
            select new ContentReviewItemDto
            {
                Id = gc.Id,
                EntityType = "GroupClass",
                Title = gc.Title,
                Description = gc.Description,
                OwnerName = u != null ? u.DisplayName : "Unknown",
                CreatedAt = gc.CreatedAt
            }
        ).Take(50).ToListAsync();

        // Combine all, sort by CreatedAt desc, take top 50
        var combined = mentorProfiles
            .Concat(courses)
            .Concat(groupClasses)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToList();

        return Ok(combined);
    }
}
