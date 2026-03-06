using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionRequests.Queries.GetMentorSessionRequests;

public record GetMentorSessionRequestsQuery : IRequest<Result<List<SessionRequestDto>>>;

public class GetMentorSessionRequestsQueryHandler : IRequestHandler<GetMentorSessionRequestsQuery, Result<List<SessionRequestDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorSessionRequestsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<SessionRequestDto>>> Handle(GetMentorSessionRequestsQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<SessionRequestDto>>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        var items = await _context.SessionRequests
            .AsNoTracking()
            .Where(sr => sr.MentorUserId == mentorUserId)
            .OrderByDescending(sr => sr.CreatedAt)
            .Select(sr => new SessionRequestDto(
                sr.Id,
                sr.StudentUserId,
                sr.Student != null ? sr.Student.DisplayName : null,
                sr.Student != null ? sr.Student.AvatarUrl : null,
                sr.MentorUserId,
                sr.Mentor != null ? sr.Mentor.DisplayName : null,
                sr.Mentor != null ? sr.Mentor.AvatarUrl : null,
                sr.OfferingId,
                sr.Offering != null ? sr.Offering.Title : null,
                sr.RequestedStartAt,
                sr.DurationMin,
                sr.StudentNote,
                sr.Status.ToString(),
                sr.RejectionReason,
                sr.BookingId,
                sr.CreatedAt))
            .ToListAsync(ct);

        return Result<List<SessionRequestDto>>.Success(items);
    }
}
