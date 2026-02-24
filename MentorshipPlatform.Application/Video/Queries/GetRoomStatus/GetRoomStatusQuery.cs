using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Queries.GetRoomStatus;

public record GetRoomStatusQuery(string RoomName) : IRequest<Result<RoomStatusDto>>;

public record RoomStatusDto(
    string RoomName,
    bool IsActive,
    bool HostConnected,
    int ParticipantCount);

public class GetRoomStatusQueryHandler
    : IRequestHandler<GetRoomStatusQuery, Result<RoomStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IVideoService _videoService;

    public GetRoomStatusQueryHandler(
        IApplicationDbContext context,
        IVideoService videoService)
    {
        _context = context;
        _videoService = videoService;
    }

    public async Task<Result<RoomStatusDto>> Handle(
        GetRoomStatusQuery request,
        CancellationToken cancellationToken)
    {
        // Check if a non-Ended VideoSession exists (prefer Live, then Scheduled)
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .Where(s => s.RoomName == request.RoomName && s.Status != VideoSessionStatus.Ended)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Fallback: try to find by extracting ResourceId from room name (e.g., group-class-{guid})
        if (session == null && request.RoomName.StartsWith("group-class-"))
        {
            var idPart = request.RoomName.Replace("group-class-", "");
            if (Guid.TryParse(idPart, out var classId))
            {
                session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .Where(s => s.ResourceId == classId && s.Status != VideoSessionStatus.Ended)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        // If DB says session is Live, use DB data for host/participant info
        if (session != null && session.Status == VideoSessionStatus.Live)
        {
            var participantCount = session.Participants.Count(p => !p.LeftAt.HasValue);
            var hostConnected = await CheckHostConnectedFromDb(session, request.RoomName, cancellationToken);

            // If DB says host is connected, trust it
            if (hostConnected)
            {
                return Result<RoomStatusDto>.Success(new RoomStatusDto(
                    request.RoomName,
                    IsActive: true,
                    HostConnected: true,
                    ParticipantCount: participantCount
                ));
            }
        }

        // Fallback: check actual Twilio room status
        // This handles cases where DB is out of sync (e.g., session created in older deploy,
        // MarkAsLive wasn't called, webhooks didn't fire, etc.)
        var (twilioExists, twilioInProgress, twilioParticipants) =
            await _videoService.GetRoomInfoAsync(request.RoomName, cancellationToken);

        if (twilioInProgress && twilioParticipants > 0)
        {
            // Twilio room is active with participants
            // Ancak DB session Ended ise, bu bir stale room — rapor etme ama tekrar Live yapma
            if (session != null && session.Status == VideoSessionStatus.Ended)
            {
                // DB'de session kapalı, Twilio'da hâlâ açık — stale room, kapatmayı dene
                try { await _videoService.CompleteRoomAsync(request.RoomName, cancellationToken); } catch { }
                return Result<RoomStatusDto>.Success(new RoomStatusDto(
                    request.RoomName,
                    IsActive: false,
                    HostConnected: false,
                    ParticipantCount: 0
                ));
            }

            // DB session Live değilse ama Scheduled ise → mentor'ün odayı aktifleştirdiğini varsay
            if (session != null && session.Status == VideoSessionStatus.Scheduled)
            {
                session.MarkAsLive();
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<RoomStatusDto>.Success(new RoomStatusDto(
                request.RoomName,
                IsActive: true,
                HostConnected: true,
                ParticipantCount: twilioParticipants
            ));
        }

        // Neither DB nor Twilio shows an active room
        return Result<RoomStatusDto>.Success(new RoomStatusDto(
            request.RoomName,
            IsActive: false,
            HostConnected: false,
            ParticipantCount: 0
        ));
    }

    private async Task<bool> CheckHostConnectedFromDb(
        Domain.Entities.VideoSession session,
        string roomName,
        CancellationToken cancellationToken)
    {
        Guid? mentorUserId = null;

        if (session.ResourceType == "Booking")
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == session.ResourceId, cancellationToken);
            mentorUserId = booking?.MentorUserId;
        }
        else if (session.ResourceType == "GroupClass")
        {
            var groupClass = await _context.GroupClasses
                .FirstOrDefaultAsync(g => g.Id == session.ResourceId, cancellationToken);
            if (groupClass != null)
            {
                mentorUserId = groupClass.MentorUserId;
            }
            else if (roomName.StartsWith("group-class-"))
            {
                var classIdStr = roomName.Replace("group-class-", "");
                if (Guid.TryParse(classIdStr, out var classId))
                {
                    var gc = await _context.GroupClasses
                        .FirstOrDefaultAsync(g => g.Id == classId, cancellationToken);
                    mentorUserId = gc?.MentorUserId;
                }
            }
        }

        if (!mentorUserId.HasValue) return false;

        return session.Participants
            .Any(p => p.UserId == mentorUserId.Value && !p.LeftAt.HasValue);
    }
}
