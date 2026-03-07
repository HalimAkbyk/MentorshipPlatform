using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Queries.GetStudentAssignments;

public record StudentAssignmentDto(
    Guid Id,
    string Title,
    string MentorName,
    string AssignmentType,
    string? DifficultyLevel,
    DateTime? DueDate,
    int? MaxScore,
    string Status,
    string? MySubmissionStatus,
    int? MyScore);

public record GetStudentAssignmentsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<StudentAssignmentDto>>>;

public class GetStudentAssignmentsQueryHandler : IRequestHandler<GetStudentAssignmentsQuery, Result<PaginatedList<StudentAssignmentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentAssignmentsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<StudentAssignmentDto>>> Handle(GetStudentAssignmentsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<StudentAssignmentDto>>.Failure("User not authenticated");

        var page = PaginatedList<StudentAssignmentDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<StudentAssignmentDto>.ClampPageSize(request.PageSize);

        var studentId = _currentUser.UserId.Value;

        // Get mentor IDs this student has bookings with
        var mentorIdsFromBookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.StudentUserId == studentId)
            .Select(b => b.MentorUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get mentor IDs from group class enrollments
        var mentorIdsFromEnrollments = await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.StudentUserId == studentId)
            .Join(_context.GroupClasses, e => e.ClassId, gc => gc.Id, (e, gc) => gc.MentorUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var allMentorIds = mentorIdsFromBookings.Union(mentorIdsFromEnrollments).Distinct().ToList();

        // Get published assignments from those mentors, or directly linked to student's bookings
        var studentBookingIds = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.StudentUserId == studentId)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var query = _context.Assignments
            .AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Published || a.Status == AssignmentStatus.Closed)
            .Where(a =>
                allMentorIds.Contains(a.MentorUserId) ||
                (a.BookingId.HasValue && studentBookingIds.Contains(a.BookingId.Value)));

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Title.Contains(request.Search));

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StudentAssignmentDto(
                x.Id,
                x.Title,
                x.Mentor.DisplayName,
                x.AssignmentType.ToString(),
                x.DifficultyLevel != null ? x.DifficultyLevel.ToString() : null,
                x.DueDate,
                x.MaxScore,
                x.Status.ToString(),
                x.Submissions
                    .Where(s => s.StudentUserId == studentId)
                    .Select(s => s.Status.ToString())
                    .FirstOrDefault(),
                x.Submissions
                    .Where(s => s.StudentUserId == studentId && s.Review != null)
                    .Select(s => s.Review!.Score)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<StudentAssignmentDto>>.Success(
            new PaginatedList<StudentAssignmentDto>(items, totalCount, page, pageSize));
    }
}
