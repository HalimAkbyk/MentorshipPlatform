namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IAdminNotificationService
{
    /// <summary>
    /// Creates a new admin notification or updates an existing unread one with the same groupKey.
    /// If an unread notification with the same groupKey exists, its count is incremented and message updated.
    /// </summary>
    Task CreateOrUpdateGroupedAsync(
        string type,
        string groupKey,
        Func<int, (string title, string message)> messageFactory,
        string? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);
}
