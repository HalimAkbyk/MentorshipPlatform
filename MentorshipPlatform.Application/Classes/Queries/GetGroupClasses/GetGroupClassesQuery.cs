using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Queries.GetGroupClasses;

public record GroupClassListDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    string? CoverImageUrl,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    int EnrolledCount,
    decimal PricePerSeat,
    string Currency,
    string Status,
    string MentorName,
    string? MentorAvatar,
    Guid MentorUserId,
    string? CurrentUserEnrollmentStatus);

public record GetGroupClassesQuery(
    string? Category,
    string? Search,
    string? SortBy,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<GroupClassListDto>>>;

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public class GetGroupClassesQueryHandler
    : IRequestHandler<GetGroupClassesQuery, Result<PagedResult<GroupClassListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGroupClassesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<GroupClassListDto>>> Handle(
        GetGroupClassesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.GroupClasses
            .Include(c => c.Enrollments)
            .Where(c => c.Status == ClassStatus.Published && c.StartAt > DateTime.UtcNow)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(c => c.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(c => c.Title.Contains(request.Search) ||
                                     (c.Description != null && c.Description.Contains(request.Search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var mentorIds = await query.Select(c => c.MentorUserId).Distinct().ToListAsync(cancellationToken);
        var mentors = await _context.Users
            .Where(u => mentorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        // Get current user's enrollments for these classes
        var userId = _currentUser.UserId;
        Dictionary<Guid, string> userEnrollments = new();
        if (userId.HasValue)
        {
            var classIds = await query.Select(c => c.Id).ToListAsync(cancellationToken);
            userEnrollments = await _context.ClassEnrollments
                .Where(e => e.StudentUserId == userId.Value &&
                            classIds.Contains(e.ClassId) &&
                            e.Status != EnrollmentStatus.Cancelled &&
                            e.Status != EnrollmentStatus.Refunded)
                .GroupBy(e => e.ClassId)
                .Select(g => new { ClassId = g.Key, Status = g.OrderByDescending(e => e.CreatedAt).First().Status })
                .ToDictionaryAsync(x => x.ClassId, x => x.Status.ToString(), cancellationToken);
        }

        // Sorting
        IOrderedQueryable<Domain.Entities.GroupClass> sorted = request.SortBy?.ToLower() switch
        {
            "price_low" => query.OrderBy(c => c.PricePerSeat),
            "price_high" => query.OrderByDescending(c => c.PricePerSeat),
            "popular" => query.OrderByDescending(c => c.Enrollments.Count(e =>
                e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.Attended)),
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            _ => query.OrderBy(c => c.StartAt), // default: soonest first
        };

        var items = await sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new GroupClassListDto(
                c.Id,
                c.Title,
                c.Description,
                c.Category,
                c.CoverImageUrl,
                c.StartAt,
                c.EndAt,
                c.Capacity,
                c.Enrollments.Count(e =>
                    e.Status == EnrollmentStatus.Confirmed ||
                    e.Status == EnrollmentStatus.Attended),
                c.PricePerSeat,
                c.Currency,
                c.Status.ToString(),
                "", // placeholder for mentor name
                null, // placeholder for mentor avatar
                c.MentorUserId,
                null)) // placeholder for enrollment status
            .ToListAsync(cancellationToken);

        // Enrich with mentor info and enrollment status
        var enriched = items.Select(i =>
        {
            var mentor = mentors.GetValueOrDefault(i.MentorUserId);
            var enrollmentStatus = userEnrollments.GetValueOrDefault(i.Id);
            return i with
            {
                MentorName = mentor?.DisplayName ?? "Mentor",
                MentorAvatar = mentor?.AvatarUrl,
                CurrentUserEnrollmentStatus = enrollmentStatus
            };
        }).ToList();

        return Result<PagedResult<GroupClassListDto>>.Success(
            new PagedResult<GroupClassListDto>(
                enriched,
                totalCount,
                request.Page,
                totalPages,
                request.Page > 1,
                request.Page < totalPages));
    }
}
