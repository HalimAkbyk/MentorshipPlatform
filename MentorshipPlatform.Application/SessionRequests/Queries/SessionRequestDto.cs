namespace MentorshipPlatform.Application.SessionRequests.Queries;

public record SessionRequestDto(
    Guid Id,
    Guid StudentUserId,
    string? StudentName,
    string? StudentAvatar,
    Guid MentorUserId,
    string? MentorName,
    string? MentorAvatar,
    Guid OfferingId,
    string? OfferingTitle,
    DateTime RequestedStartAt,
    int DurationMin,
    string? StudentNote,
    string Status,
    string? RejectionReason,
    Guid? BookingId,
    DateTime CreatedAt);
