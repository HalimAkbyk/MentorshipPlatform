namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IAgoraWhiteboardService
{
    Task<WhiteboardRoomResult> CreateRoomAsync(string roomName, CancellationToken ct = default);
    Task<string> GenerateRoomTokenAsync(string roomUuid, string userId, bool isWriter, CancellationToken ct = default);
}

public record WhiteboardRoomResult(bool Success, string? RoomUuid, string? ErrorMessage);
