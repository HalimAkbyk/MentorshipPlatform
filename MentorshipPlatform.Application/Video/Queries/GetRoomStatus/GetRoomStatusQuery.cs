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

    public GetRoomStatusQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RoomStatusDto>> Handle(
        GetRoomStatusQuery request,
        CancellationToken cancellationToken)
    {
        // Check if VideoSession exists and is Live
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomName == request.RoomName, cancellationToken);

        // Fallback: try to find by extracting ResourceId from room name (e.g., group-class-{guid})
        if (session == null && request.RoomName.StartsWith("group-class-"))
        {
            var idPart = request.RoomName.Replace("group-class-", "");
            if (Guid.TryParse(idPart, out var classId))
            {
                session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .FirstOrDefaultAsync(s => s.ResourceId == classId, cancellationToken);
            }
        }

        if (session == null)
        {
            // Room doesn't exist yet - mentor hasn't activated
            return Result<RoomStatusDto>.Success(new RoomStatusDto(
                request.RoomName,
                IsActive: false,
                HostConnected: false,
                ParticipantCount: 0
            ));
        }

        // Room exists - check if it's Live
        var isActive = session.Status == VideoSessionStatus.Live;
        var participantCount = session.Participants.Count(p => !p.LeftAt.HasValue);

        // Determine if host (mentor) is actually connected right now
        // by checking if the mentor has an active participant record (no LeftAt)
        var hostConnected = false;
        if (isActive)
        {
            // Resolve mentor UserId based on resource type
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
                // If ResourceId is Guid.Empty (legacy), try to resolve from room name
                if (groupClass != null)
                {
                    mentorUserId = groupClass.MentorUserId;
                }
                else
                {
                    // For group-class-{classId} format rooms where ResourceId was empty
                    var roomName = request.RoomName;
                    if (roomName.StartsWith("group-class-"))
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
            }

            if (mentorUserId.HasValue)
            {
                // Check if mentor has an active participant record (joined but not left)
                hostConnected = session.Participants
                    .Any(p => p.UserId == mentorUserId.Value && !p.LeftAt.HasValue);
            }
        }

        var dto = new RoomStatusDto(
            request.RoomName,
            IsActive: isActive,
            HostConnected: hostConnected,
            ParticipantCount: participantCount
        );

        return Result<RoomStatusDto>.Success(dto);
    }
}