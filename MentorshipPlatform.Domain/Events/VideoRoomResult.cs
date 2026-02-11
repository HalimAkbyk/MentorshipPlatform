namespace MentorshipPlatform.Domain.Events;

public record VideoRoomResult(bool Success, string RoomName, string? ErrorMessage);