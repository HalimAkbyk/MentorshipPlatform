using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Queries.GetMyEnrollments;

public record MyEnrollmentDto(
    Guid EnrollmentId,
    Guid ClassId,
    string ClassTitle,
    string? ClassDescription,
    string Category,
    string? CoverImageUrl,
    DateTime StartAt,
    DateTime EndAt,
    decimal PricePerSeat,
    string Currency,
    string ClassStatus,
    string EnrollmentStatus,
    string MentorName,
    string? MentorAvatar,
    Guid MentorUserId,
    DateTime EnrolledAt);

public record GetMyEnrollmentsQuery(int Page = 1, int PageSize = 15) : IRequest<Result<PaginatedList<MyEnrollmentDto>>>;

public class GetMyEnrollmentsQueryHandler
    : IRequestHandler<GetMyEnrollmentsQuery, Result<PaginatedList<MyEnrollmentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyEnrollmentsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<MyEnrollmentDto>>> Handle(
        GetMyEnrollmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<MyEnrollmentDto>>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        var page = PaginatedList<MyEnrollmentDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<MyEnrollmentDto>.ClampPageSize(request.PageSize);

        var baseQuery = _context.ClassEnrollments
            .Include(e => e.Class)
            .Where(e => e.StudentUserId == studentUserId)
            .OrderByDescending(e => e.Class.StartAt);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var enrollments = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var mentorIds = enrollments.Select(e => e.Class.MentorUserId).Distinct().ToList();
        var mentors = await _context.Users
            .Where(u => mentorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = enrollments.Select(e =>
        {
            var mentor = mentors.GetValueOrDefault(e.Class.MentorUserId);
            return new MyEnrollmentDto(
                e.Id,
                e.ClassId,
                e.Class.Title,
                e.Class.Description,
                e.Class.Category,
                e.Class.CoverImageUrl,
                e.Class.StartAt,
                e.Class.EndAt,
                e.Class.PricePerSeat,
                e.Class.Currency,
                e.Class.Status.ToString(),
                e.Status.ToString(),
                mentor?.DisplayName ?? "Mentor",
                mentor?.AvatarUrl,
                e.Class.MentorUserId,
                e.CreatedAt);
        }).ToList();

        return Result<PaginatedList<MyEnrollmentDto>>.Success(
            new PaginatedList<MyEnrollmentDto>(items, totalCount, page, pageSize));
    }
}
