namespace MentorshipPlatform.Domain.Events;

public record VideoTokenResult(bool Success, string? Token, string? ErrorMessage);