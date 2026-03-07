using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class SessionPlan : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string? Title { get; private set; }
    public Guid? BookingId { get; private set; }
    public Guid? GroupClassId { get; private set; }
    public Guid? CurriculumTopicId { get; private set; }
    public string? PreSessionNote { get; private set; }
    public string? SessionObjective { get; private set; }
    public string? SessionNotes { get; private set; }
    public string? AgendaItemsJson { get; private set; }
    public string? PostSessionSummary { get; private set; }
    public Guid? LinkedAssignmentId { get; private set; }
    public SessionPlanStatus Status { get; private set; }
    public DateTime? SharedAt { get; private set; }

    // Navigation
    public User Mentor { get; private set; } = null!;
    public Booking? Booking { get; private set; }
    public GroupClass? GroupClass { get; private set; }
    public ICollection<SessionPlanMaterial> Materials { get; private set; } = new List<SessionPlanMaterial>();

    private SessionPlan() { }

    public static SessionPlan Create(
        Guid mentorUserId,
        string? title,
        Guid? bookingId = null,
        Guid? groupClassId = null,
        Guid? curriculumTopicId = null,
        string? preSessionNote = null,
        string? sessionObjective = null)
    {
        return new SessionPlan
        {
            MentorUserId = mentorUserId,
            Title = title,
            BookingId = bookingId,
            GroupClassId = groupClassId,
            CurriculumTopicId = curriculumTopicId,
            PreSessionNote = preSessionNote,
            SessionObjective = sessionObjective,
            Status = SessionPlanStatus.Draft
        };
    }

    public void Update(
        string? title,
        string? preSessionNote,
        string? sessionObjective,
        string? sessionNotes,
        string? agendaItemsJson,
        string? postSessionSummary,
        Guid? linkedAssignmentId)
    {
        Title = title;
        PreSessionNote = preSessionNote;
        SessionObjective = sessionObjective;
        SessionNotes = sessionNotes;
        AgendaItemsJson = agendaItemsJson;
        PostSessionSummary = postSessionSummary;
        LinkedAssignmentId = linkedAssignmentId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Share()
    {
        SharedAt = DateTime.UtcNow;
        Status = SessionPlanStatus.Shared;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = SessionPlanStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }
}
