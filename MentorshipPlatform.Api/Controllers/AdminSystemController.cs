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
}
