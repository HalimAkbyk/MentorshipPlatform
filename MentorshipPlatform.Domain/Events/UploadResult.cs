namespace MentorshipPlatform.Domain.Events;

public record UploadResult(bool Success, string? FileKey, string? PublicUrl, string? ErrorMessage);