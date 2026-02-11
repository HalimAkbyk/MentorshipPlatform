using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
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

    public CreateVideoSessionCommandHandler(
        IApplicationDbContext context,
        IVideoService videoService)
    {
        _context = context;
        _videoService = videoService;
    }

    public async Task<Result<VideoSessionDto>> Handle(
        CreateVideoSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Check if session already exists
        var existing = await _context.VideoSessions
            .FirstOrDefaultAsync(s =>
                    s.ResourceType == request.ResourceType &&
                    s.ResourceId == request.ResourceId,
                cancellationToken);

        if (existing != null)
        {
            return Result<VideoSessionDto>.Success(
                new VideoSessionDto(existing.Id, existing.RoomName));
        }

        // Create room via Twilio
        var roomResult = await _videoService.CreateRoomAsync(
            request.ResourceType,
            request.ResourceId,
            cancellationToken);

        if (!roomResult.Success)
            return Result<VideoSessionDto>.Failure(roomResult.ErrorMessage ?? "Failed to create video room");

        // Save session
        var session = VideoSession.Create(
            request.ResourceType,
            request.ResourceId,
            roomResult.RoomName);

        _context.VideoSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<VideoSessionDto>.Success(
            new VideoSessionDto(session.Id, session.RoomName));
    }
}