using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Queries.GetMyGroupClasses;

public record MyGroupClassDto(
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
    string Status);

public record GetMyGroupClassesQuery(string? Status, int Page = 1, int PageSize = 15) : IRequest<Result<PaginatedList<MyGroupClassDto>>>;

public class GetMyGroupClassesQueryHandler
    : IRequestHandler<GetMyGroupClassesQuery, Result<PaginatedList<MyGroupClassDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyGroupClassesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<MyGroupClassDto>>> Handle(
        GetMyGroupClassesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<MyGroupClassDto>>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        var query = _context.GroupClasses
            .Include(c => c.Enrollments)
            .Where(c => c.MentorUserId == mentorUserId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ClassStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(c => c.Status == statusFilter);
        }

        var page = PaginatedList<MyGroupClassDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<MyGroupClassDto>.ClampPageSize(request.PageSize);

        var totalCount = await query.CountAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var classes = await query
            .OrderByDescending(c => c.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new MyGroupClassDto(
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
                // Virtual status: if Published but end time passed → "Expired"
                c.Status == ClassStatus.Published && c.EndAt < now
                    ? "Expired"
                    : c.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<MyGroupClassDto>>.Success(
            new PaginatedList<MyGroupClassDto>(classes, totalCount, page, pageSize));
    }
}
