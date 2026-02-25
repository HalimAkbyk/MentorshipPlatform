using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Commands.CreateVideoSession;

public record CreateVideoSessionCommand(
    string ResourceType,
    Guid ResourceId) : IRequest<Result<VideoSessionDto>>;

public record VideoSessionDto(Guid SessionId, string RoomName);

public class CreateVideoSessionCommandHandler
    : IRequestHandler<CreateVideoSessionCommand, Result<VideoSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IVideoService _videoService;
    private readonly IChatNotificationService _chatNotification;

    public CreateVideoSessionCommandHandler(
        IApplicationDbContext context,
        IVideoService videoService,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _videoService = videoService;
        _chatNotification = chatNotification;
    }

    public async Task<Result<VideoSessionDto>> Handle(
        CreateVideoSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Check if a Live/Scheduled session already exists for this resource
        var existing = await _context.VideoSessions
            .FirstOrDefaultAsync(s =>
                    s.ResourceType == request.ResourceType &&
                    s.ResourceId == request.ResourceId &&
                    s.Status != VideoSessionStatus.Ended,
                cancellationToken);

        if (existing != null)
        {
            return Result<VideoSessionDto>.Success(
                new VideoSessionDto(existing.Id, existing.RoomName));
        }

        // Determine room name matching frontend convention
        var expectedRoomName = request.ResourceType switch
        {
            "GroupClass" => $"group-class-{request.ResourceId}",
            _ => $"{request.ResourceType}-{request.ResourceId}"
        };

        // Create room via Twilio (may already exist if previous session ended)
        var roomResult = await _videoService.CreateRoomAsync(
            request.ResourceType,
            request.ResourceId,
            cancellationToken);

        // If Twilio room already exists, use our expected room name
        var roomName = roomResult.Success ? roomResult.RoomName : expectedRoomName;

        if (!roomResult.Success && !roomResult.ErrorMessage!.Contains("Room exists", System.StringComparison.OrdinalIgnoreCase))
            return Result<VideoSessionDto>.Failure(roomResult.ErrorMessage ?? "Failed to create video room");

        // Save session
        var session = VideoSession.Create(
            request.ResourceType,
            request.ResourceId,
            roomName);

        _context.VideoSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify students that a session has been created (room is being prepared)
        await NotifySessionCreated(request.ResourceType, request.ResourceId, roomName, cancellationToken);

        return Result<VideoSessionDto>.Success(
            new VideoSessionDto(session.Id, session.RoomName));
    }

    private async Task NotifySessionCreated(
        string resourceType, Guid resourceId, string roomName, CancellationToken cancellationToken)
    {
        try
        {
            var studentUserIds = new List<Guid>();

            if (resourceType == "Booking")
            {
                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == resourceId, cancellationToken);
                if (booking != null)
                    studentUserIds.Add(booking.StudentUserId);
            }
            else if (resourceType == "GroupClass")
            {
                var enrolledStudents = await _context.ClassEnrollments
                    .Where(e => e.ClassId == resourceId && e.Status == EnrollmentStatus.Confirmed)
                    .Select(e => e.StudentUserId)
                    .ToListAsync(cancellationToken);
                studentUserIds.AddRange(enrolledStudents);
            }

            // Session just created = Scheduled, host not yet connected, 0 participants
            // But the mentor is about to join, so this signals "room is being prepared"
            foreach (var studentId in studentUserIds)
            {
                await _chatNotification.NotifyRoomStatusChanged(
                    studentId, roomName, isActive: false, hostConnected: false, participantCount: 0);
            }
        }
        catch
        {
            // Non-critical
        }
    }
}