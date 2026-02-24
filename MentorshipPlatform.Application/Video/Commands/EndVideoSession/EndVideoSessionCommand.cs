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
    private readonly IVideoService _videoService;

    public EndVideoSessionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IVideoService videoService)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _videoService = videoService;
    }

    public async Task<Result> Handle(
        EndVideoSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        // Search by room name first, then try fallback for group classes
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomName == request.RoomName &&
                                      s.Status != VideoSessionStatus.Ended, cancellationToken);

        // Fallback: try to find by extracting ResourceId from room name (e.g., group-class-{guid})
        if (session == null && request.RoomName.StartsWith("group-class-"))
        {
            var idPart = request.RoomName.Replace("group-class-", "");
            if (Guid.TryParse(idPart, out var classId))
            {
                session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .FirstOrDefaultAsync(s => s.ResourceId == classId &&
                                              s.Status != VideoSessionStatus.Ended, cancellationToken);
            }
        }

        if (session == null)
            return Result.Failure("Session not found");

        var oldStatus = session.Status.ToString();

        // Mark session as ended in DB
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

        // ──── Twilio room'u da kapat (stale room önleme) ────
        try
        {
            await _videoService.CompleteRoomAsync(request.RoomName, cancellationToken);
        }
        catch
        {
            // Twilio kapanmazsa bile DB session'ı kapalı — kritik değil
        }

        await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
            oldStatus, "Ended",
            $"Session sonlandırıldı. Room: {request.RoomName}, {activeParticipants.Count} aktif katılımcı çıkartıldı, Twilio room kapatıldı",
            _currentUser.UserId, "Mentor", ct: cancellationToken);

        return Result.Success();
    }
}
