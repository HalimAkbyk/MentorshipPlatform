using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionRequests.Queries.GetAdminSessionRequests;

public record GetAdminSessionRequestsQuery(string? Status = null) : IRequest<Result<List<SessionRequestDto>>>;

public class GetAdminSessionRequestsQueryHandler : IRequestHandler<GetAdminSessionRequestsQuery, Result<List<SessionRequestDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminSessionRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SessionRequestDto>>> Handle(GetAdminSessionRequestsQuery request, CancellationToken ct)
    {
        var query = _context.SessionRequests
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<SessionRequestStatus>(request.Status, true, out var status))
        {
            query = query.Where(sr => sr.Status == status);
        }

        var items = await query
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
