using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IVideoService
{
    Task<VideoRoomResult> CreateRoomAsync(
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken = default);

    Task<VideoTokenResult> GenerateTokenAsync(
        string roomName,
        Guid userId,
        string participantName,
        bool isHost = false,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveParticipantAsync(
        string roomName,
        string participantIdentity,
        CancellationToken cancellationToken = default);
}