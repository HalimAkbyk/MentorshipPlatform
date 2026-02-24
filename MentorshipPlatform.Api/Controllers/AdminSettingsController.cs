namespace MentorshipPlatform.Api.Controllers;

using MediatR;
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

public class TestEmailRequest
{
    public string Email { get; set; } = string.Empty;
}

// Category DTOs
public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public string EntityType { get; set; } = "General";
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public string EntityType { get; set; } = "General";
}

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformSettingService _settingsService;
    private readonly IMediator _mediator;
    private readonly IEmailService _emailService;

    public AdminSettingsController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IPlatformSettingService settingsService,
        IMediator mediator,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _mediator = mediator;
        _settingsService = settingsService;
        _emailService = emailService;
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

        // Invalidate settings cache so changes take effect immediately
        _settingsService.InvalidateCache();

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
    // TEST EMAIL
    // -----------------------------

    [HttpPost("email/test")]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { errors = new[] { "Email is required." } });

        await _emailService.SendRawEmailAsync(
            request.Email,
            "Test E-posta - MentorHub",
            "<html><body style='font-family:Arial,sans-serif;'><h2>Test E-postas\u0131 \u2705</h2><p>E-posta ayarlar\u0131n\u0131z do\u011fru yap\u0131land\u0131r\u0131lm\u0131\u015ft\u0131r.</p><p style='color:#6b7280;font-size:12px;'>Bu bir test mesaj\u0131d\u0131r.</p></body></html>");

        return Ok(new { message = "Test e-postas\u0131 g\u00f6nderildi." });
    }

    // -----------------------------
    // SEED DEFAULT SETTINGS
    // -----------------------------

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDefaultSettings()
    {
        var defaults = new List<(string Key, string Value, string Description, string Category)>
        {
            // ── General ──
            ("platform_name", "Degisim Mentorluk", "Platform goruntuleme adi", "General"),
            ("platform_description", "Mentor ve ogrenci eslestirme platformu", "Platform aciklamasi", "General"),
            ("support_email", "destek@degisimmentorluk.com", "Destek e-posta adresi", "General"),
            ("frontend_url", "https://mentorshipplatformreact.vercel.app", "Frontend URL adresi", "General"),

            // ── Fee / Commission ──
            ("platform_commission_rate", "0.07", "1:1 ders rezervasyonlari icin platform komisyon orani (0.07 = %7)", "Fee"),
            ("mentor_commission_rate", "0.15", "Mentor komisyon kesintisi orani (0.15 = %15)", "Fee"),
            ("course_commission_rate", "0.07", "Kurs ve grup dersleri icin platform komisyon orani (0.07 = %7)", "Fee"),

            // ── Email (SMTP) ──
            ("smtp_host", "", "SMTP sunucu adresi (orn: smtp.gmail.com)", "Email"),
            ("smtp_port", "587", "SMTP port numarasi", "Email"),
            ("smtp_username", "", "SMTP kullanici adi", "Email"),
            ("smtp_password", "", "SMTP sifresi", "Email"),
            ("smtp_from_email", "", "Gonderici e-posta adresi", "Email"),
            ("smtp_from_name", "Degisim Mentorluk", "Gonderici adi", "Email"),
            ("email_provider", "smtp", "E-posta sa\u011flay\u0131c\u0131 (smtp veya resend)", "Email"),
            ("resend_api_key", "", "Resend API anahtar\u0131", "Email"),
            ("resend_from_email", "", "Resend g\u00f6nderici e-posta", "Email"),
            ("resend_from_name", "MentorHub", "Resend g\u00f6nderici ad\u0131", "Email"),

            // ── SMS ──
            ("sms_enabled", "false", "SMS bildirimlerini ac/kapat", "SMS"),

            // ── Payment ──
            ("payment_provider", "iyzico", "Odeme saglayici (iyzico)", "Payment"),

            // ── Limits ──
            ("max_booking_per_day", "10", "Bir mentor icin gunluk maksimum ders sayisi", "Limits"),
            ("max_class_capacity", "50", "Grup dersi maksimum katilimci sayisi", "Limits"),
            ("default_session_duration_min", "60", "Varsayilan ders suresi (dakika)", "Limits"),
            ("booking_auto_expire_minutes", "30", "Odenmemis rezervasyonlarin otomatik iptal suresi (dakika)", "Limits"),
            ("minimum_payout_amount", "100", "Mentorlarin talep edebilecegi minimum odeme tutari (TRY)", "Limits"),

            // ── Session Lifecycle ──
            ("session_grace_period_minutes", "10", "1:1 ders bittikten sonra uzatma suresi (dakika)", "Limits"),
            ("group_class_grace_period_minutes", "15", "Grup dersi bittikten sonra uzatma suresi (dakika)", "Limits"),
            ("noshow_wait_minutes", "15", "Karsi taraf gelmezse no-show tespit suresi (dakika)", "Limits"),

            // ── Dev / Debug ──
            ("dev_mode_session_bypass", "true", "Gelistirme modu: Acikken ders herhangi bir zamanda baslatilabilir", "Dev"),
            ("session_early_join_minutes", "15", "Ders baslangicina kac dakika kala katilim acilir", "Dev"),
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

    // ========================================
    // CATEGORY MANAGEMENT ENDPOINTS
    // ========================================

    // GET ALL CATEGORIES (admin)
    [HttpGet("/api/admin/categories")]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
    {
        var items = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.EntityType)
            .ThenBy(c => c.SortOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                EntityType = c.EntityType,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // CREATE CATEGORY
    [HttpPost("/api/admin/categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { errors = new[] { "Name is required." } });

        var category = Category.Create(request.Name, request.Icon, request.SortOrder, request.EntityType);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            EntityType = category.EntityType,
            CreatedAt = category.CreatedAt
        });
    }

    // UPDATE CATEGORY
    [HttpPut("/api/admin/categories/{id:guid}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { errors = new[] { "Name is required." } });

        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(new { errors = new[] { "Category not found." } });

        category.Update(request.Name, request.Icon, request.SortOrder, request.EntityType);
        await _db.SaveChangesAsync();

        return Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            EntityType = category.EntityType,
            CreatedAt = category.CreatedAt
        });
    }

    // DELETE CATEGORY (only if unused)
    [HttpDelete("/api/admin/categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(new { errors = new[] { "Category not found." } });

        // Check if category is in use by GroupClasses, Courses, or Offerings
        var usedByGroupClass = await _db.GroupClasses.AnyAsync(g => g.Category == category.Name);
        var usedByCourse = await _db.Courses.AnyAsync(c => c.Category == category.Name);
        var usedByOffering = await _db.Offerings.AnyAsync(o => o.Category == category.Name);

        if (usedByGroupClass || usedByCourse || usedByOffering)
            return BadRequest(new { errors = new[] { "Category is in use and cannot be deleted. Deactivate it instead." } });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ACTIVATE CATEGORY
    [HttpPost("/api/admin/categories/{id:guid}/activate")]
    public async Task<IActionResult> ActivateCategory(Guid id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(new { errors = new[] { "Category not found." } });

        category.Activate();
        await _db.SaveChangesAsync();

        return Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            EntityType = category.EntityType,
            CreatedAt = category.CreatedAt
        });
    }

    // DEACTIVATE CATEGORY
    [HttpPost("/api/admin/categories/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateCategory(Guid id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(new { errors = new[] { "Category not found." } });

        category.Deactivate();
        await _db.SaveChangesAsync();

        return Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            EntityType = category.EntityType,
            CreatedAt = category.CreatedAt
        });
    }

    // ========================================
    // GLOBAL SEARCH ENDPOINT
    // ========================================

    // GET /api/admin/search?q=xxx
    [HttpGet("/api/admin/search")]
    public async Task<IActionResult> GlobalSearch([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new { users = Array.Empty<object>(), courses = Array.Empty<object>(), groupClasses = Array.Empty<object>(), orders = Array.Empty<object>() });

        var term = q.Trim().ToLower();

        // Search users (top 5)
        var users = await _db.Users
            .Where(u => u.DisplayName.ToLower().Contains(term) || u.Email.ToLower().Contains(term))
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.AvatarUrl, Roles = u.Roles })
            .ToListAsync();

        // Search courses (top 5)
        var courses = await _db.Courses
            .Where(c => c.Title.ToLower().Contains(term))
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new { c.Id, c.Title, Status = c.Status.ToString(), c.Category })
            .ToListAsync();

        // Search group classes (top 5)
        var groupClasses = await _db.GroupClasses
            .Where(gc => gc.Title.ToLower().Contains(term))
            .OrderByDescending(gc => gc.CreatedAt)
            .Take(5)
            .Select(gc => new { gc.Id, gc.Title, Status = gc.Status.ToString(), gc.Category })
            .ToListAsync();

        // Search orders by providerPaymentId (top 5)
        var orders = await _db.Orders
            .Where(o => o.ProviderPaymentId != null && o.ProviderPaymentId.ToLower().Contains(term))
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new { o.Id, Type = o.Type.ToString(), Status = o.Status.ToString(), o.AmountTotal, o.ProviderPaymentId })
            .ToListAsync();

        return Ok(new { users, courses, groupClasses, orders });
    }

    // ========================================
    // ADMIN PENDING COUNTS
    // ========================================

    [HttpGet("/api/admin/pending-counts")]
    public async Task<IActionResult> GetPendingCounts(CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Admin.Queries.GetAdminPendingCounts.GetAdminPendingCountsQuery(), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // ========================================
    // ADMIN NOTIFICATION ENDPOINTS
    // ========================================

    // GET /api/admin/notifications
    [HttpGet("/api/admin/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.AdminNotifications.AsQueryable();
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.IsRead,
                n.ReferenceType,
                n.ReferenceId,
                n.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { items, total });
    }

    // GET /api/admin/notifications/unread-count
    [HttpGet("/api/admin/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadNotificationCount()
    {
        var count = await _db.AdminNotifications.CountAsync(n => !n.IsRead);
        return Ok(new { count });
    }

    // POST /api/admin/notifications/{id}/read
    [HttpPost("/api/admin/notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(Guid id)
    {
        var notification = await _db.AdminNotifications.FindAsync(id);
        if (notification == null) return NotFound();
        notification.MarkAsRead();
        await _db.SaveChangesAsync(default);
        return Ok();
    }

    // POST /api/admin/notifications/read-all
    [HttpPost("/api/admin/notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        await _db.AdminNotifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.UpdatedAt, DateTime.UtcNow));
        return Ok();
    }
}
