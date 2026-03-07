using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class SessionPlanMaterial : BaseEntity
{
    public Guid SessionPlanId { get; private set; }
    public Guid LibraryItemId { get; private set; }
    public SessionPhase Phase { get; private set; }
    public int SortOrder { get; private set; }
    public string? Note { get; private set; }

    // Navigation
    public SessionPlan SessionPlan { get; private set; } = null!;
    public LibraryItem LibraryItem { get; private set; } = null!;

    private SessionPlanMaterial() { }

    public static SessionPlanMaterial Create(
        Guid sessionPlanId,
        Guid libraryItemId,
        SessionPhase phase,
        int sortOrder,
        string? note = null)
    {
        return new SessionPlanMaterial
        {
            SessionPlanId = sessionPlanId,
            LibraryItemId = libraryItemId,
            Phase = phase,
            SortOrder = sortOrder,
            Note = note
        };
    }
}
