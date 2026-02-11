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

        var dto = new RoomStatusDto(
            request.RoomName,
            IsActive: isActive,
            HostConnected: isActive, // If Live, host has connected
            ParticipantCount: participantCount
        );

        return Result<RoomStatusDto>.Success(dto);
    }
}