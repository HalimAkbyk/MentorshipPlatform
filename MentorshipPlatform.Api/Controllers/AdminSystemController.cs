using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Admin.Queries.GetAllUsers;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Api.Controllers;

// ────────────────────────── DTOs ──────────────────────────

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Description { get; set; }
    public Guid? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public string? PerformedByRole { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SystemHealthDto
{
    public string Status { get; set; } = "Healthy";
    public string DatabaseStatus { get; set; } = "Unknown";
    public int TotalUsers { get; set; }
    public int TotalOrders { get; set; }
    public DateTime ServerTime { get; set; }
    public string Environment { get; set; } = string.Empty;
}

public class FeatureFlagDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ToggleFeatureFlagRequest
{
    public bool IsEnabled { get; set; }
}

public class AuditSessionSummaryDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MentorName { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int ParticipantCount { get; set; }
    public DateTime LastEventAt { get; set; }
}

public class AuditSessionDetailDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MentorName { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<AuditEventDto> Events { get; set; } = new();
    public List<AuditParticipantDto> Participants { get; set; } = new();
}

public class AuditEventDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? PerformedByName { get; set; }
    public string? PerformedByRole { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditParticipantDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int TotalDurationSec { get; set; }
    public int SegmentCount { get; set; }
    public List<ParticipantSegmentDto> Segments { get; set; } = new();
}

public class ParticipantSegmentDto
{
    public Guid SegmentId { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int DurationSec { get; set; }
}

public class AuditUserSummaryDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int TotalActions { get; set; }
    public DateTime LastActionAt { get; set; }
    public int SessionCount { get; set; }
    public int TotalSessionDurationSec { get; set; }
}

public class AuditUserDetailDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<UserSessionDto> Sessions { get; set; } = new();
    public List<AuditEventDto> RecentActions { get; set; } = new();
    public int TotalActions { get; set; }
}

public class UserSessionDto
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int DurationSec { get; set; }
    public string Role { get; set; } = string.Empty;
    public int SegmentCount { get; set; }
    public List<ParticipantSegmentDto> Segments { get; set; } = new();
}

// ────────────────────────── Controller ──────────────────────────

[ApiController]
[Route("api/admin/system")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminSystemController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IFeatureFlagService _featureFlagService;

    public AdminSystemController(
        IApplicationDbContext context,
        IWebHostEnvironment env,
        IFeatureFlagService featureFlagService)
    {
        _context = context;
        _env = env;
        _featureFlagService = featureFlagService;
    }

    /// <summary>
    /// Paginated audit log from ProcessHistories with optional filters.
    /// </summary>
    [HttpGet("audit-log")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var query = _context.ProcessHistories.AsNoTracking().AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(p => p.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(p => p.Action == action);

        if (dateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= dateTo.Value);

        // Total count
        var totalCount = await query.CountAsync(ct);

        // Pagination
        var pg = Math.Max(1, page);
        var ps = Math.Clamp(pageSize, 1, 100);
        var skip = (pg - 1) * ps;

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(ps)
            .ToListAsync(ct);

        // Get performer names
        var performerIds = items
            .Where(p => p.PerformedBy.HasValue)
            .Select(p => p.PerformedBy!.Value)
            .Distinct()
            .ToList();

        var performerNames = await _context.Users
            .AsNoTracking()
            .Where(u => performerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var dtos = items.Select(p =>
        {
            string? performerName = null;
            if (p.PerformedBy.HasValue)
                performerNames.TryGetValue(p.PerformedBy.Value, out performerName);

            return new AuditLogDto
            {
                Id = p.Id,
                EntityType = p.EntityType,
                EntityId = p.EntityId,
                Action = p.Action,
                OldValue = p.OldValue,
                NewValue = p.NewValue,
                Description = p.Description,
                PerformedBy = p.PerformedBy,
                PerformedByName = performerName,
                PerformedByRole = p.PerformedByRole,
                CreatedAt = p.CreatedAt
            };
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / ps);

        return Ok(new PagedResult<AuditLogDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = pg,
            PageSize = ps,
            TotalPages = totalPages
        });
    }

    /// <summary>
    /// System health check: database connectivity, basic counts, server info.
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthDto>> GetHealth(CancellationToken ct)
    {
        var dto = new SystemHealthDto
        {
            ServerTime = DateTime.UtcNow,
            Environment = _env.EnvironmentName
        };

        try
        {
            dto.TotalUsers = await _context.Users.AsNoTracking().CountAsync(ct);
            dto.TotalOrders = await _context.Orders.AsNoTracking().CountAsync(ct);
            dto.DatabaseStatus = "Healthy";
            dto.Status = "Healthy";
        }
        catch (Exception)
        {
            dto.DatabaseStatus = "Unhealthy";
            dto.Status = "Degraded";
        }

        return Ok(dto);
    }

    /// <summary>
    /// List all feature flags.
    /// </summary>
    [HttpGet("feature-flags")]
    public async Task<ActionResult<List<FeatureFlagDto>>> GetFeatureFlags(CancellationToken ct)
    {
        var flags = await _context.FeatureFlags
            .AsNoTracking()
            .OrderBy(f => f.Key)
            .Select(f => new FeatureFlagDto
            {
                Id = f.Id,
                Key = f.Key,
                IsEnabled = f.IsEnabled,
                Description = f.Description,
                UpdatedAt = f.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(flags);
    }

    /// <summary>
    /// Toggle a feature flag by key. Creates the flag if it doesn't exist.
    /// </summary>
    [HttpPut("feature-flags/{key}")]
    public async Task<ActionResult<FeatureFlagDto>> ToggleFeatureFlag(
        [FromRoute] string key,
        [FromBody] ToggleFeatureFlagRequest request,
        CancellationToken ct)
    {
        var flag = await _context.FeatureFlags
            .FirstOrDefaultAsync(f => f.Key == key, ct);

        if (flag == null)
        {
            flag = FeatureFlag.Create(key, request.IsEnabled, null);
            _context.FeatureFlags.Add(flag);
        }
        else
        {
            flag.SetEnabled(request.IsEnabled);
        }

        await _context.SaveChangesAsync(ct);

        // Invalidate the feature flag cache so changes take effect immediately
        _featureFlagService.InvalidateCache();

        return Ok(new FeatureFlagDto
        {
            Id = flag.Id,
            Key = flag.Key,
            IsEnabled = flag.IsEnabled,
            Description = flag.Description,
            UpdatedAt = flag.UpdatedAt
        });
    }

    /// <summary>
    /// Seed default feature flags if none exist.
    /// </summary>
    [HttpPost("feature-flags/seed")]
    public async Task<ActionResult<List<FeatureFlagDto>>> SeedFeatureFlags(CancellationToken ct)
    {
        var existingCount = await _context.FeatureFlags.AsNoTracking().CountAsync(ct);
        if (existingCount > 0)
        {
            return Ok(await _context.FeatureFlags.AsNoTracking()
                .OrderBy(f => f.Key)
                .Select(f => new FeatureFlagDto
                {
                    Id = f.Id,
                    Key = f.Key,
                    IsEnabled = f.IsEnabled,
                    Description = f.Description,
                    UpdatedAt = f.UpdatedAt
                })
                .ToListAsync(ct));
        }

        var defaults = new List<(string key, bool enabled, string description)>
        {
            ("registration_enabled", true, "Yeni kullan\u0131c\u0131 kay\u0131tlar\u0131n\u0131 a\u00e7/kapat"),
            ("course_sales_enabled", true, "Kurs sat\u0131\u015flar\u0131n\u0131 a\u00e7/kapat"),
            ("group_classes_enabled", true, "Grup derslerini a\u00e7/kapat"),
            ("chat_enabled", true, "Mesajla\u015fma \u00f6zelli\u011fini a\u00e7/kapat"),
            ("video_enabled", true, "Video g\u00f6r\u00fc\u015fme \u00f6zelli\u011fini a\u00e7/kapat"),
            ("maintenance_mode", false, "Bak\u0131m modu"),
        };

        var flags = new List<FeatureFlag>();
        foreach (var (key, enabled, description) in defaults)
        {
            var flag = FeatureFlag.Create(key, enabled, description);
            _context.FeatureFlags.Add(flag);
            flags.Add(flag);
        }

        await _context.SaveChangesAsync(ct);

        var result = flags.Select(f => new FeatureFlagDto
        {
            Id = f.Id,
            Key = f.Key,
            IsEnabled = f.IsEnabled,
            Description = f.Description,
            UpdatedAt = f.UpdatedAt
        }).ToList();

        return Ok(result);
    }

    // ────────────────────────── Audit Log – Session Endpoints ──────────────────────────

    /// <summary>
    /// Paginated list of audit sessions (Bookings + GroupClasses) with event counts.
    /// </summary>
    [HttpGet("audit-log/sessions")]
    public async Task<ActionResult<PagedResult<AuditSessionSummaryDto>>> GetAuditSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        try
        {
            var pg = Math.Max(1, page);
            var ps = Math.Clamp(pageSize, 1, 100);

            // 1. Gather all Booking-based summaries
            var bookingSummaries = new List<AuditSessionSummaryDto>();
            if (string.IsNullOrWhiteSpace(type) || type == "Booking")
            {
                var bookingQuery = _context.Bookings.AsNoTracking()
                    .Include(b => b.Offering)
                    .Include(b => b.Student)
                    .Include(b => b.Mentor)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    bookingQuery = bookingQuery.Where(b =>
                        b.Mentor.DisplayName.ToLower().Contains(s) ||
                        b.Student.DisplayName.ToLower().Contains(s));
                }

                var bookings = await bookingQuery.ToListAsync(ct);
                var bookingIds = bookings.Select(b => b.Id).ToList();

                // Get direct ProcessHistory events for bookings
                var bookingEvents = await _context.ProcessHistories.AsNoTracking()
                    .Where(p => p.EntityType == "Booking" && bookingIds.Contains(p.EntityId))
                    .GroupBy(p => p.EntityId)
                    .Select(g => new
                    {
                        EntityId = g.Key,
                        TotalEvents = g.Count(),
                        ParticipantCount = g.Select(p => p.PerformedBy).Distinct().Count(),
                        LastEventAt = g.Max(p => p.CreatedAt)
                    })
                    .ToDictionaryAsync(x => x.EntityId, ct);

                // Get VideoSession-related events: VideoSessions where ResourceType == "Booking"
                var videoSessionsForBookings = await _context.VideoSessions.AsNoTracking()
                    .Where(vs => vs.ResourceType == "Booking" && bookingIds.Contains(vs.ResourceId))
                    .ToListAsync(ct);

                var videoSessionIds = videoSessionsForBookings.Select(vs => vs.Id).ToList();
                var videoEventsByResource = new Dictionary<Guid, (int Count, int Participants, DateTime MaxDate)>();
                if (videoSessionIds.Any())
                {
                    var videoEvents = await _context.ProcessHistories.AsNoTracking()
                        .Where(p => p.EntityType == "VideoSession" && videoSessionIds.Contains(p.EntityId))
                        .ToListAsync(ct);

                    // Map video session ID -> resource ID
                    var vsIdToResourceId = videoSessionsForBookings.ToDictionary(vs => vs.Id, vs => vs.ResourceId);

                    foreach (var grp in videoEvents.GroupBy(p => p.EntityId))
                    {
                        if (vsIdToResourceId.TryGetValue(grp.Key, out var resourceId))
                        {
                            var count = grp.Count();
                            var participants = grp.Select(p => p.PerformedBy).Distinct().Count();
                            var maxDate = grp.Max(p => p.CreatedAt);

                            if (videoEventsByResource.ContainsKey(resourceId))
                            {
                                var existing = videoEventsByResource[resourceId];
                                videoEventsByResource[resourceId] = (
                                    existing.Count + count,
                                    Math.Max(existing.Participants, participants),
                                    existing.MaxDate > maxDate ? existing.MaxDate : maxDate);
                            }
                            else
                            {
                                videoEventsByResource[resourceId] = (count, participants, maxDate);
                            }
                        }
                    }
                }

                foreach (var b in bookings)
                {
                    bookingEvents.TryGetValue(b.Id, out var be);
                    videoEventsByResource.TryGetValue(b.Id, out var ve);

                    var totalEvents = (be?.TotalEvents ?? 0) + ve.Count;
                    var participantCount = Math.Max(be?.ParticipantCount ?? 0, ve.Participants);
                    var lastEvent = be != null && ve.MaxDate != default
                        ? (be.LastEventAt > ve.MaxDate ? be.LastEventAt : ve.MaxDate)
                        : be?.LastEventAt ?? ve.MaxDate;

                    if (totalEvents == 0) continue; // Skip bookings with no audit events

                    bookingSummaries.Add(new AuditSessionSummaryDto
                    {
                        EntityId = b.Id,
                        EntityType = "Booking",
                        Title = b.Offering?.Title ?? "Untitled",
                        MentorName = b.Mentor?.DisplayName ?? "Unknown",
                        StudentName = b.Student?.DisplayName,
                        ScheduledDate = b.StartAt,
                        Status = b.Status.ToString(),
                        TotalEvents = totalEvents,
                        ParticipantCount = participantCount,
                        LastEventAt = lastEvent
                    });
                }
            }

            // 2. Gather all GroupClass-based summaries
            var groupClassSummaries = new List<AuditSessionSummaryDto>();
            if (string.IsNullOrWhiteSpace(type) || type == "GroupClass")
            {
                var gcQuery = _context.GroupClasses.AsNoTracking().AsQueryable();

                var gcList = await gcQuery.ToListAsync(ct);
                var gcIds = gcList.Select(g => g.Id).ToList();
                var mentorIds = gcList.Select(g => g.MentorUserId).Distinct().ToList();

                var mentorNames = await _context.Users.AsNoTracking()
                    .Where(u => mentorIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

                // Get direct events
                var gcEvents = await _context.ProcessHistories.AsNoTracking()
                    .Where(p => p.EntityType == "GroupClass" && gcIds.Contains(p.EntityId))
                    .GroupBy(p => p.EntityId)
                    .Select(g => new
                    {
                        EntityId = g.Key,
                        TotalEvents = g.Count(),
                        ParticipantCount = g.Select(p => p.PerformedBy).Distinct().Count(),
                        LastEventAt = g.Max(p => p.CreatedAt)
                    })
                    .ToDictionaryAsync(x => x.EntityId, ct);

                // Get VideoSession-related events for GroupClasses
                var videoSessionsForGc = await _context.VideoSessions.AsNoTracking()
                    .Where(vs => vs.ResourceType == "GroupClass" && gcIds.Contains(vs.ResourceId))
                    .ToListAsync(ct);

                var gcVideoSessionIds = videoSessionsForGc.Select(vs => vs.Id).ToList();
                var gcVideoEventsByResource = new Dictionary<Guid, (int Count, int Participants, DateTime MaxDate)>();
                if (gcVideoSessionIds.Any())
                {
                    var gcVideoEvents = await _context.ProcessHistories.AsNoTracking()
                        .Where(p => p.EntityType == "VideoSession" && gcVideoSessionIds.Contains(p.EntityId))
                        .ToListAsync(ct);

                    var gcVsIdToResourceId = videoSessionsForGc.ToDictionary(vs => vs.Id, vs => vs.ResourceId);

                    foreach (var grp in gcVideoEvents.GroupBy(p => p.EntityId))
                    {
                        if (gcVsIdToResourceId.TryGetValue(grp.Key, out var resourceId))
                        {
                            var count = grp.Count();
                            var participants = grp.Select(p => p.PerformedBy).Distinct().Count();
                            var maxDate = grp.Max(p => p.CreatedAt);

                            if (gcVideoEventsByResource.ContainsKey(resourceId))
                            {
                                var existing = gcVideoEventsByResource[resourceId];
                                gcVideoEventsByResource[resourceId] = (
                                    existing.Count + count,
                                    Math.Max(existing.Participants, participants),
                                    existing.MaxDate > maxDate ? existing.MaxDate : maxDate);
                            }
                            else
                            {
                                gcVideoEventsByResource[resourceId] = (count, participants, maxDate);
                            }
                        }
                    }
                }

                // Apply search filter on mentor names for group classes
                foreach (var gc in gcList)
                {
                    mentorNames.TryGetValue(gc.MentorUserId, out var mentorName);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.ToLower();
                        if (!(mentorName?.ToLower().Contains(s) == true ||
                              gc.Title.ToLower().Contains(s)))
                            continue;
                    }

                    gcEvents.TryGetValue(gc.Id, out var ge);
                    gcVideoEventsByResource.TryGetValue(gc.Id, out var gve);

                    var totalEvents = (ge?.TotalEvents ?? 0) + gve.Count;
                    var participantCount = Math.Max(ge?.ParticipantCount ?? 0, gve.Participants);
                    var lastEvent = ge != null && gve.MaxDate != default
                        ? (ge.LastEventAt > gve.MaxDate ? ge.LastEventAt : gve.MaxDate)
                        : ge?.LastEventAt ?? gve.MaxDate;

                    if (totalEvents == 0) continue;

                    groupClassSummaries.Add(new AuditSessionSummaryDto
                    {
                        EntityId = gc.Id,
                        EntityType = "GroupClass",
                        Title = gc.Title,
                        MentorName = mentorName ?? "Unknown",
                        StudentName = null,
                        ScheduledDate = gc.StartAt,
                        Status = gc.Status.ToString(),
                        TotalEvents = totalEvents,
                        ParticipantCount = participantCount,
                        LastEventAt = lastEvent
                    });
                }
            }

            // 3. Combine, sort by LastEventAt desc, paginate
            var allSummaries = bookingSummaries
                .Concat(groupClassSummaries)
                .OrderByDescending(s => s.LastEventAt)
                .ToList();

            var totalCount = allSummaries.Count;
            var skip = (pg - 1) * ps;
            var pagedItems = allSummaries.Skip(skip).Take(ps).ToList();
            var totalPages = (int)Math.Ceiling((double)totalCount / ps);

            return Ok(new PagedResult<AuditSessionSummaryDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = pg,
                PageSize = ps,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve audit sessions", detail = ex.Message });
        }
    }

    /// <summary>
    /// Detailed audit trail for a specific session (Booking or GroupClass) including video events.
    /// </summary>
    [HttpGet("audit-log/sessions/{entityId}")]
    public async Task<ActionResult<AuditSessionDetailDto>> GetAuditSessionDetail(
        [FromRoute] Guid entityId,
        [FromQuery] string entityType = "Booking",
        CancellationToken ct = default)
    {
        try
        {
            // 1. Get direct ProcessHistory events for this entity
            var directEvents = await _context.ProcessHistories.AsNoTracking()
                .Where(p => p.EntityType == entityType && p.EntityId == entityId)
                .ToListAsync(ct);

            // 2. Get VideoSession-related events
            var videoSessions = await _context.VideoSessions.AsNoTracking()
                .Where(vs => vs.ResourceId == entityId)
                .ToListAsync(ct);

            var videoSessionIds = videoSessions.Select(vs => vs.Id).ToList();
            var videoEvents = new List<ProcessHistory>();
            if (videoSessionIds.Any())
            {
                videoEvents = await _context.ProcessHistories.AsNoTracking()
                    .Where(p => p.EntityType == "VideoSession" && videoSessionIds.Contains(p.EntityId))
                    .ToListAsync(ct);
            }

            // 3. Combine all events
            var allEvents = directEvents.Concat(videoEvents).OrderBy(e => e.CreatedAt).ToList();

            // 4. Get performer names
            var performerIds = allEvents
                .Where(e => e.PerformedBy.HasValue)
                .Select(e => e.PerformedBy!.Value)
                .Distinct()
                .ToList();

            var performerNames = await _context.Users.AsNoTracking()
                .Where(u => performerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

            // 5. Build event DTOs
            var eventDtos = allEvents.Select(e =>
            {
                string? performerName = null;
                if (e.PerformedBy.HasValue)
                    performerNames.TryGetValue(e.PerformedBy.Value, out performerName);

                return new AuditEventDto
                {
                    Id = e.Id,
                    EntityType = e.EntityType,
                    Action = e.Action,
                    Description = e.Description,
                    OldValue = e.OldValue,
                    NewValue = e.NewValue,
                    PerformedByName = performerName,
                    PerformedByRole = e.PerformedByRole,
                    CreatedAt = e.CreatedAt
                };
            }).ToList();

            // 6. Build participant list from VideoParticipant records (segment-based)
            // Each VideoParticipant row = one join/leave segment with its own ID
            var videoParticipantRecords = new List<VideoParticipant>();
            if (videoSessionIds.Any())
            {
                videoParticipantRecords = await _context.VideoParticipants.AsNoTracking()
                    .Where(vp => videoSessionIds.Contains(vp.VideoSessionId))
                    .OrderBy(vp => vp.JoinedAt)
                    .ToListAsync(ct);
            }

            var participantUserIds = videoParticipantRecords
                .Select(vp => vp.UserId)
                .Distinct()
                .ToList();

            var participantUsers = await _context.Users.AsNoTracking()
                .Where(u => participantUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl, Role = u.Roles.FirstOrDefault().ToString() }, ct);

            var participants = new List<AuditParticipantDto>();
            foreach (var userId in participantUserIds)
            {
                var userSegments = videoParticipantRecords
                    .Where(vp => vp.UserId == userId)
                    .OrderBy(vp => vp.JoinedAt)
                    .ToList();

                var totalDuration = userSegments.Sum(s => s.DurationSec);
                var firstJoin = userSegments.First().JoinedAt;
                var lastLeft = userSegments.LastOrDefault(s => s.LeftAt.HasValue)?.LeftAt;

                participantUsers.TryGetValue(userId, out var userInfo);

                participants.Add(new AuditParticipantDto
                {
                    UserId = userId,
                    DisplayName = userInfo?.DisplayName ?? "Unknown",
                    AvatarUrl = userInfo?.AvatarUrl,
                    Role = userInfo?.Role ?? "Unknown",
                    JoinedAt = firstJoin,
                    LeftAt = lastLeft,
                    TotalDurationSec = Math.Max(0, totalDuration),
                    SegmentCount = userSegments.Count,
                    Segments = userSegments.Select(s => new ParticipantSegmentDto
                    {
                        SegmentId = s.Id,
                        JoinedAt = s.JoinedAt,
                        LeftAt = s.LeftAt,
                        DurationSec = s.DurationSec
                    }).ToList()
                });
            }

            // 7. Build header info from Booking or GroupClass
            string title = "Unknown", mentorName = "Unknown", status = "Unknown";
            string? studentName = null;
            DateTime? scheduledDate = null;

            if (entityType == "Booking")
            {
                var booking = await _context.Bookings.AsNoTracking()
                    .Include(b => b.Offering)
                    .Include(b => b.Mentor)
                    .Include(b => b.Student)
                    .FirstOrDefaultAsync(b => b.Id == entityId, ct);

                if (booking != null)
                {
                    title = booking.Offering?.Title ?? "Untitled";
                    mentorName = booking.Mentor?.DisplayName ?? "Unknown";
                    studentName = booking.Student?.DisplayName;
                    scheduledDate = booking.StartAt;
                    status = booking.Status.ToString();
                }
            }
            else if (entityType == "GroupClass")
            {
                var gc = await _context.GroupClasses.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == entityId, ct);

                if (gc != null)
                {
                    title = gc.Title;
                    scheduledDate = gc.StartAt;
                    status = gc.Status.ToString();

                    var mentor = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == gc.MentorUserId, ct);
                    mentorName = mentor?.DisplayName ?? "Unknown";
                }
            }

            return Ok(new AuditSessionDetailDto
            {
                EntityId = entityId,
                EntityType = entityType,
                Title = title,
                MentorName = mentorName,
                StudentName = studentName,
                ScheduledDate = scheduledDate,
                Status = status,
                Events = eventDtos,
                Participants = participants
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve session detail", detail = ex.Message });
        }
    }

    // ────────────────────────── Audit Log – User Endpoints ──────────────────────────

    /// <summary>
    /// Paginated list of users who have performed audit actions, with summary stats.
    /// </summary>
    [HttpGet("audit-log/users")]
    public async Task<ActionResult<PagedResult<AuditUserSummaryDto>>> GetAuditUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        try
        {
            var pg = Math.Max(1, page);
            var ps = Math.Clamp(pageSize, 1, 100);

            // 1. Group ProcessHistories by PerformedBy
            var userGroups = await _context.ProcessHistories.AsNoTracking()
                .Where(p => p.PerformedBy != null)
                .GroupBy(p => p.PerformedBy!.Value)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalActions = g.Count(),
                    LastActionAt = g.Max(p => p.CreatedAt),
                    SessionCount = g.Where(p => p.Action == "ParticipantJoined").Select(p => p.EntityId).Distinct().Count()
                })
                .ToListAsync(ct);

            // 2. Get all user IDs and fetch user info
            var userIds = userGroups.Select(g => g.UserId).ToList();
            var users = await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, Role = u.Roles.FirstOrDefault().ToString() }, ct);

            // 3. Calculate session durations and counts from VideoParticipant records (segment-based)
            var sessionDurations = new Dictionary<Guid, int>();
            var sessionCounts = new Dictionary<Guid, int>();

            if (userIds.Any())
            {
                var vpRecords = await _context.VideoParticipants.AsNoTracking()
                    .Where(vp => userIds.Contains(vp.UserId))
                    .ToListAsync(ct);

                foreach (var uid in userIds)
                {
                    var userSegments = vpRecords.Where(vp => vp.UserId == uid).ToList();
                    sessionDurations[uid] = userSegments.Sum(s => s.DurationSec);
                    sessionCounts[uid] = userSegments.Select(s => s.VideoSessionId).Distinct().Count();
                }
            }

            // 4. Build summaries
            var summaries = userGroups
                .Select(g =>
                {
                    users.TryGetValue(g.UserId, out var userInfo);
                    sessionDurations.TryGetValue(g.UserId, out var duration);

                    sessionCounts.TryGetValue(g.UserId, out var sessCount);

                    return new AuditUserSummaryDto
                    {
                        UserId = g.UserId,
                        DisplayName = userInfo?.DisplayName ?? "Unknown",
                        Role = userInfo?.Role ?? "Unknown",
                        TotalActions = g.TotalActions,
                        LastActionAt = g.LastActionAt,
                        SessionCount = sessCount,
                        TotalSessionDurationSec = duration
                    };
                })
                .ToList();

            // 5. Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                summaries = summaries
                    .Where(u => u.DisplayName.ToLower().Contains(s))
                    .ToList();
            }

            // 6. Sort by LastActionAt desc, paginate
            summaries = summaries.OrderByDescending(u => u.LastActionAt).ToList();

            var totalCount = summaries.Count;
            var skip = (pg - 1) * ps;
            var pagedItems = summaries.Skip(skip).Take(ps).ToList();
            var totalPages = (int)Math.Ceiling((double)totalCount / ps);

            return Ok(new PagedResult<AuditUserSummaryDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = pg,
                PageSize = ps,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve audit users", detail = ex.Message });
        }
    }

    /// <summary>
    /// Detailed audit trail for a specific user: their sessions and recent actions.
    /// </summary>
    [HttpGet("audit-log/users/{userId}")]
    public async Task<ActionResult<AuditUserDetailDto>> GetAuditUserDetail(
        [FromRoute] Guid userId,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Get user info
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
                return NotFound(new { error = "User not found" });

            var userRole = user.Roles.FirstOrDefault().ToString();

            // 2. Get all VideoParticipant records for this user (segment-based)
            var userParticipantRecords = await _context.VideoParticipants.AsNoTracking()
                .Where(vp => vp.UserId == userId)
                .OrderByDescending(vp => vp.JoinedAt)
                .ToListAsync(ct);

            // 3. Get related VideoSessions for title resolution
            var videoSessionIds = userParticipantRecords
                .Select(vp => vp.VideoSessionId)
                .Distinct()
                .ToList();

            var videoSessions = await _context.VideoSessions.AsNoTracking()
                .Where(vs => videoSessionIds.Contains(vs.Id))
                .ToListAsync(ct);

            var vsDict = videoSessions.ToDictionary(vs => vs.Id);

            // Gather resource IDs for title lookup
            var bookingResourceIds = videoSessions
                .Where(vs => vs.ResourceType == "Booking")
                .Select(vs => vs.ResourceId)
                .Distinct()
                .ToList();

            var gcResourceIds = videoSessions
                .Where(vs => vs.ResourceType == "GroupClass")
                .Select(vs => vs.ResourceId)
                .Distinct()
                .ToList();

            var bookingTitles = new Dictionary<Guid, string>();
            if (bookingResourceIds.Any())
            {
                bookingTitles = await _context.Bookings.AsNoTracking()
                    .Include(b => b.Offering)
                    .Where(b => bookingResourceIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id, b => b.Offering != null ? b.Offering.Title : "Untitled", ct);
            }

            var gcTitles = new Dictionary<Guid, string>();
            if (gcResourceIds.Any())
            {
                gcTitles = await _context.GroupClasses.AsNoTracking()
                    .Where(g => gcResourceIds.Contains(g.Id))
                    .ToDictionaryAsync(g => g.Id, g => g.Title, ct);
            }

            // Group by VideoSession to build per-session summaries with segments
            var sessions = new List<UserSessionDto>();
            foreach (var vsGroup in userParticipantRecords.GroupBy(vp => vp.VideoSessionId))
            {
                var segments = vsGroup.OrderBy(vp => vp.JoinedAt).ToList();
                var totalDuration = segments.Sum(s => s.DurationSec);
                var firstJoin = segments.First().JoinedAt;
                var lastLeft = segments.LastOrDefault(s => s.LeftAt.HasValue)?.LeftAt;

                // Resolve title from VideoSession -> Resource
                var sessionTitle = "Unknown";
                var sessionEntityType = "VideoSession";
                var sessionEntityId = vsGroup.Key;

                if (vsDict.TryGetValue(vsGroup.Key, out var vs))
                {
                    sessionEntityType = vs.ResourceType;
                    sessionEntityId = vs.ResourceId;

                    if (vs.ResourceType == "Booking" && bookingTitles.TryGetValue(vs.ResourceId, out var bt))
                        sessionTitle = bt;
                    else if (vs.ResourceType == "GroupClass" && gcTitles.TryGetValue(vs.ResourceId, out var gt))
                        sessionTitle = gt;
                }

                sessions.Add(new UserSessionDto
                {
                    EntityId = sessionEntityId,
                    EntityType = sessionEntityType,
                    Title = sessionTitle,
                    JoinedAt = firstJoin,
                    LeftAt = lastLeft,
                    DurationSec = Math.Max(0, totalDuration),
                    Role = userRole,
                    SegmentCount = segments.Count,
                    Segments = segments.Select(s => new ParticipantSegmentDto
                    {
                        SegmentId = s.Id,
                        JoinedAt = s.JoinedAt,
                        LeftAt = s.LeftAt,
                        DurationSec = s.DurationSec
                    }).ToList()
                });
            }

            // 4. Get recent actions (last 50)
            var recentActions = await _context.ProcessHistories.AsNoTracking()
                .Where(p => p.PerformedBy == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            var recentActionDtos = recentActions.Select(e => new AuditEventDto
            {
                Id = e.Id,
                EntityType = e.EntityType,
                Action = e.Action,
                Description = e.Description,
                OldValue = e.OldValue,
                NewValue = e.NewValue,
                PerformedByName = user.DisplayName,
                PerformedByRole = e.PerformedByRole,
                CreatedAt = e.CreatedAt
            }).ToList();

            // 5. Total action count
            var totalActions = await _context.ProcessHistories.AsNoTracking()
                .CountAsync(p => p.PerformedBy == userId, ct);

            return Ok(new AuditUserDetailDto
            {
                UserId = userId,
                DisplayName = user.DisplayName,
                Role = userRole,
                Sessions = sessions,
                RecentActions = recentActionDtos,
                TotalActions = totalActions
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve user detail", detail = ex.Message });
        }
    }
}
