using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.FreeSessions.Queries.GetMyFreeSessions;

public record FreeSessionDto(
    Guid Id,
    Guid MentorUserId,
    string MentorName,
    Guid StudentUserId,
    string StudentName,
    string RoomName,
    string Status,
    DateTime? StartedAt,
    DateTime? EndedAt,
    string? Note,
    DateTime CreatedAt);

public record GetMyFreeSessionsQuery : IRequest<Result<List<FreeSessionDto>>>;

public class GetMyFreeSessionsQueryHandler
    : IRequestHandler<GetMyFreeSessionsQuery, Result<List<FreeSessionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyFreeSessionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<FreeSessionDto>>> Handle(
        GetMyFreeSessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId!.Value;

        var sessions = await _context.FreeSessions
            .AsNoTracking()
            .Where(s => s.MentorUserId == userId || s.StudentUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new FreeSessionDto(
                s.Id,
                s.MentorUserId,
                s.Mentor.DisplayName,
                s.StudentUserId,
                s.Student.DisplayName,
                s.RoomName,
                s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
                s.Note,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<FreeSessionDto>>.Success(sessions);
    }
}
