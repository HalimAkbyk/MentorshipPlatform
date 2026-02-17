using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetAllUsers;

// DTOs
public class AdminUserDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public int? BirthYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int BookingCount { get; set; }
    public int OrderCount { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

// Query
public record GetAllUsersQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Role,
    string? Status,
    string? SortBy,
    bool SortDesc
) : IRequest<Result<PagedResult<AdminUserDto>>>;

// Handler
public class GetAllUsersQueryHandler
    : IRequestHandler<GetAllUsersQuery, Result<PagedResult<AdminUserDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<AdminUserDto>>> Handle(
        GetAllUsersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(u =>
                u.DisplayName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)));
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(request.Role) &&
            Enum.TryParse<UserRole>(request.Role, true, out var roleFilter))
        {
            query = query.Where(u => u.Roles.Contains(roleFilter));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<UserStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(u => u.Status == statusFilter);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        var sortBy = request.SortBy?.ToLower() ?? "createdat";
        query = sortBy switch
        {
            "displayname" => request.SortDesc
                ? query.OrderByDescending(u => u.DisplayName)
                : query.OrderBy(u => u.DisplayName),
            "email" => request.SortDesc
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),
            "status" => request.SortDesc
                ? query.OrderByDescending(u => u.Status)
                : query.OrderBy(u => u.Status),
            _ => request.SortDesc
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt)
        };

        // Pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var users = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Get user IDs for counting
        var userIds = users.Select(u => u.Id).ToList();

        // Count bookings per user (as student)
        var bookingCounts = await _context.Bookings
            .AsNoTracking()
            .Where(b => userIds.Contains(b.StudentUserId))
            .GroupBy(b => b.StudentUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        // Count orders per user
        var orderCounts = await _context.Orders
            .AsNoTracking()
            .Where(o => userIds.Contains(o.BuyerUserId))
            .GroupBy(o => o.BuyerUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var items = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            AvatarUrl = u.AvatarUrl,
            Phone = u.Phone,
            BirthYear = u.BirthYear,
            Status = u.Status.ToString(),
            Roles = u.Roles.Select(r => r.ToString()).ToList(),
            CreatedAt = u.CreatedAt,
            LastLoginAt = null, // User entity doesn't have LastLoginAt yet
            BookingCount = bookingCounts.TryGetValue(u.Id, out var bc) ? bc : 0,
            OrderCount = orderCounts.TryGetValue(u.Id, out var oc) ? oc : 0
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var result = new PagedResult<AdminUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };

        return Result<PagedResult<AdminUserDto>>.Success(result);
    }
}
