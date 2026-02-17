using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminNotificationController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;

    public AdminNotificationController(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        INotificationService notificationService)
    {
        _context = context;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    // -----------------------------
    // DTOs
    // -----------------------------

    public class NotificationTemplateDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Variables { get; set; }
        public string Channel { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BulkNotificationDto
    {
        public Guid Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int RecipientCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SentByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // -----------------------------
    // Request Models
    // -----------------------------

    public class CreateTemplateRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Variables { get; set; }
        public string Channel { get; set; } = "Email";
    }

    public class UpdateTemplateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Variables { get; set; }
    }

    public class SendBulkNotificationRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = "All";
        public string Channel { get; set; } = "Email";
        public DateTime? ScheduledAt { get; set; }
    }

    // -----------------------------
    // TEMPLATES
    // -----------------------------

    /// <summary>
    /// List all notification templates.
    /// </summary>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        var templates = await _context.NotificationTemplates
            .AsNoTracking()
            .OrderBy(t => t.Key)
            .Select(t => new NotificationTemplateDto
            {
                Id = t.Id,
                Key = t.Key,
                Name = t.Name,
                Subject = t.Subject,
                Body = t.Body,
                Variables = t.Variables,
                Channel = t.Channel,
                IsActive = t.IsActive,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(templates);
    }

    /// <summary>
    /// Create a new notification template.
    /// </summary>
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        var exists = await _context.NotificationTemplates
            .AsNoTracking()
            .AnyAsync(t => t.Key == request.Key, ct);

        if (exists)
            return BadRequest(new { errors = new[] { $"Template with key '{request.Key}' already exists." } });

        var template = NotificationTemplate.Create(
            request.Key,
            request.Name,
            request.Subject,
            request.Body,
            request.Variables,
            request.Channel);

        _context.NotificationTemplates.Add(template);
        await _context.SaveChangesAsync(ct);

        return Ok(new NotificationTemplateDto
        {
            Id = template.Id,
            Key = template.Key,
            Name = template.Name,
            Subject = template.Subject,
            Body = template.Body,
            Variables = template.Variables,
            Channel = template.Channel,
            IsActive = template.IsActive,
            UpdatedAt = template.UpdatedAt
        });
    }

    /// <summary>
    /// Update an existing notification template.
    /// </summary>
    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate([FromRoute] Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken ct)
    {
        var template = await _context.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template == null)
            return NotFound();

        template.Update(request.Name, request.Subject, request.Body, request.Variables);
        await _context.SaveChangesAsync(ct);

        return Ok(new NotificationTemplateDto
        {
            Id = template.Id,
            Key = template.Key,
            Name = template.Name,
            Subject = template.Subject,
            Body = template.Body,
            Variables = template.Variables,
            Channel = template.Channel,
            IsActive = template.IsActive,
            UpdatedAt = template.UpdatedAt
        });
    }

    /// <summary>
    /// Delete a notification template.
    /// </summary>
    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate([FromRoute] Guid id, CancellationToken ct)
    {
        var template = await _context.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template == null)
            return NotFound();

        _context.NotificationTemplates.Remove(template);
        await _context.SaveChangesAsync(ct);

        return Ok();
    }

    // -----------------------------
    // BULK NOTIFICATION HISTORY
    // -----------------------------

    /// <summary>
    /// List bulk notification history (paginated).
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = from bn in _context.BulkNotifications.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on bn.SentByUserId equals u.Id into uj
                    from u in uj.DefaultIfEmpty()
                    orderby bn.CreatedAt descending
                    select new BulkNotificationDto
                    {
                        Id = bn.Id,
                        Subject = bn.Subject,
                        Body = bn.Body,
                        TargetAudience = bn.TargetAudience,
                        Channel = bn.Channel,
                        ScheduledAt = bn.ScheduledAt,
                        SentAt = bn.SentAt,
                        RecipientCount = bn.RecipientCount,
                        Status = bn.Status,
                        SentByName = u != null ? u.DisplayName : null,
                        CreatedAt = bn.CreatedAt
                    };

        var totalCount = await _context.BulkNotifications.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    // -----------------------------
    // SEND BULK NOTIFICATION
    // -----------------------------

    /// <summary>
    /// Send a bulk notification to target audience.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendBulkNotification([FromBody] SendBulkNotificationRequest request, CancellationToken ct)
    {
        var currentUserId = _currentUser.UserId;
        if (!currentUserId.HasValue)
            return Unauthorized();

        var notification = BulkNotification.Create(
            request.Subject,
            request.Body,
            request.TargetAudience,
            request.Channel,
            request.ScheduledAt,
            currentUserId.Value);

        _context.BulkNotifications.Add(notification);

        // If scheduled for later, just save and return
        if (request.ScheduledAt.HasValue)
        {
            await _context.SaveChangesAsync(ct);
            return Ok(new { id = notification.Id, status = notification.Status, recipientCount = 0 });
        }

        // Immediate send: get target users
        var usersQuery = _context.Users.AsNoTracking()
            .Where(u => u.Status == UserStatus.Active && u.Email != null);

        switch (request.TargetAudience)
        {
            case "Students":
                usersQuery = usersQuery.Where(u => u.Roles.Any(r => r == UserRole.Student));
                break;
            case "Mentors":
                usersQuery = usersQuery.Where(u => u.Roles.Any(r => r == UserRole.Mentor));
                break;
            // "All" - no additional filter
        }

        var recipients = await usersQuery
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        notification.MarkAsSending(recipients.Count);
        await _context.SaveChangesAsync(ct);

        try
        {
            foreach (var recipient in recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient.Email))
                {
                    await _notificationService.SendEmailAsync(
                        recipient.Email,
                        request.Subject,
                        request.Body,
                        ct);
                }
            }

            notification.MarkAsSent();
        }
        catch
        {
            notification.MarkAsFailed();
        }

        await _context.SaveChangesAsync(ct);

        return Ok(new { id = notification.Id, status = notification.Status, recipientCount = recipients.Count });
    }
}
