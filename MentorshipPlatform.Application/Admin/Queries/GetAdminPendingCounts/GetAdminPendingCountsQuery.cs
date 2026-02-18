using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetAdminPendingCounts;

public record AdminPendingCountsDto(int PendingVerifications, int PendingCourseReviews);

public record GetAdminPendingCountsQuery() : IRequest<Result<AdminPendingCountsDto>>;

public class GetAdminPendingCountsQueryHandler : IRequestHandler<GetAdminPendingCountsQuery, Result<AdminPendingCountsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminPendingCountsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminPendingCountsDto>> Handle(GetAdminPendingCountsQuery request, CancellationToken cancellationToken)
    {
        var pendingVerifications = await _context.MentorVerifications
            .CountAsync(v => v.Status == VerificationStatus.Pending, cancellationToken);

        var pendingCourseReviews = await _context.Courses
            .CountAsync(c => c.Status == CourseStatus.PendingReview, cancellationToken);

        return Result<AdminPendingCountsDto>.Success(
            new AdminPendingCountsDto(pendingVerifications, pendingCourseReviews));
    }
}
