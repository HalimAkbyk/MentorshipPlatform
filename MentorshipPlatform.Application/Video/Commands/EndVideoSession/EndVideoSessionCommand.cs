using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Commands.EndVideoSession;

public record EndVideoSessionCommand(string RoomName) : IRequest<Result>;

public class EndVideoSessionCommandHandler : IRequestHandler<EndVideoSessionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public EndVideoSessionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result> Handle(
        EndVideoSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomName == request.RoomName, cancellationToken);

        if (session == null)
            return Result.Failure("Session not found");

        var oldStatus = session.Status.ToString();

        // Mark session as ended
        session.MarkAsEnded();

        // Mark all active participants as left
        var activeParticipants = await _context.VideoParticipants
            .Where(p => p.VideoSessionId == session.Id && !p.LeftAt.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var participant in activeParticipants)
        {
            participant.Leave();
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
            oldStatus, "Ended",
            $"Session sonlandırıldı. Room: {request.RoomName}, {activeParticipants.Count} aktif katılımcı çıkartıldı",
            _currentUser.UserId, "Mentor", ct: cancellationToken);

        return Result.Success();
    }
}
