namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IProcessHistoryService
{
    Task LogAsync(
        string entityType,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        string description,
        Guid? performedBy = null,
        string? performedByRole = null,
        string? metadata = null,
        CancellationToken ct = default);
}
