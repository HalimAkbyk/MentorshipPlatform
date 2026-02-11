using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class ProcessHistory : BaseEntity
{
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? PerformedBy { get; private set; }
    public string? PerformedByRole { get; private set; }
    public string? Metadata { get; private set; }

    private ProcessHistory() { }

    public static ProcessHistory Create(
        string entityType,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        string description,
        Guid? performedBy = null,
        string? performedByRole = null,
        string? metadata = null)
    {
        return new ProcessHistory
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Description = description,
            PerformedBy = performedBy,
            PerformedByRole = performedByRole,
            Metadata = metadata
        };
    }
}
