using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
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

public record GetMyGroupClassesQuery(string? Status) : IRequest<Result<List<MyGroupClassDto>>>;

public class GetMyGroupClassesQueryHandler
    : IRequestHandler<GetMyGroupClassesQuery, Result<List<MyGroupClassDto>>>
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

    public async Task<Result<List<MyGroupClassDto>>> Handle(
        GetMyGroupClassesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<MyGroupClassDto>>.Failure("User not authenticated");

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

        var classes = await query
            .OrderByDescending(c => c.StartAt)
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
                c.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Result<List<MyGroupClassDto>>.Success(classes);
    }
}
