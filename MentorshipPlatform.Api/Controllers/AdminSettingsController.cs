namespace MentorshipPlatform.Api.Controllers;

using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// DTOs
public class PlatformSettingDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class UpdateSettingRequest
{
    public string Value { get; set; } = string.Empty;
}

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdminSettingsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // -----------------------------
    // GET ALL SETTINGS
    // -----------------------------

    [HttpGet]
    public async Task<ActionResult<List<PlatformSettingDto>>> GetSettings()
    {
        var items = await _db.PlatformSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .Select(s => new PlatformSettingDto
            {
                Id = s.Id,
                Key = s.Key,
                Value = s.Value,
                Description = s.Description,
                Category = s.Category,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // -----------------------------
    // UPDATE SETTING BY KEY
    // -----------------------------

    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateSetting([FromRoute] string key, [FromBody] UpdateSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
            return BadRequest(new { errors = new[] { "Value is required." } });

        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Unauthorized();

        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            // Create a new setting with Category = "General" if not found
            setting = PlatformSetting.Create(key, request.Value, null, "General");
            _db.PlatformSettings.Add(setting);
        }
        else
        {
            setting.UpdateValue(request.Value, userId.Value);
        }

        await _db.SaveChangesAsync();

        return Ok(new PlatformSettingDto
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            Description = setting.Description,
            Category = setting.Category,
            UpdatedAt = setting.UpdatedAt
        });
    }

    // -----------------------------
    // SEED DEFAULT SETTINGS
    // -----------------------------

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDefaultSettings()
    {
        var defaults = new List<(string Key, string Value, string Description, string Category)>
        {
            ("platform_name", "MentorHub", "Platform display name", "General"),
            ("maintenance_mode", "false", "Enable/disable maintenance mode", "General"),
            ("platform_commission_rate", "0.07", "Platform commission rate for bookings", "Fee"),
            ("mentor_commission_rate", "0.15", "Mentor commission rate", "Fee"),
            ("course_commission_rate", "0.07", "Platform commission rate for courses", "Fee"),
            ("registration_open", "true", "Whether new user registration is open", "Registration"),
            ("email_verification_required", "true", "Whether email verification is required for new users", "Registration"),
        };

        var existingKeys = await _db.PlatformSettings
            .AsNoTracking()
            .Select(s => s.Key)
            .ToListAsync();

        var newSettings = new List<PlatformSetting>();

        foreach (var (key, value, description, category) in defaults)
        {
            if (!existingKeys.Contains(key))
            {
                newSettings.Add(PlatformSetting.Create(key, value, description, category));
            }
        }

        if (newSettings.Any())
        {
            _db.PlatformSettings.AddRange(newSettings);
            await _db.SaveChangesAsync();
        }

        return Ok(new { seeded = newSettings.Count, skipped = defaults.Count - newSettings.Count });
    }
}
