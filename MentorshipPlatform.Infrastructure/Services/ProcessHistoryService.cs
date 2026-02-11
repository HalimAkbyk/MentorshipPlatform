using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

public class ProcessHistoryService : IProcessHistoryService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProcessHistoryService> _logger;

    public ProcessHistoryService(
        IApplicationDbContext context,
        ILogger<ProcessHistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        string entityType,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        string description,
        Guid? performedBy = null,
        string? performedByRole = null,
        string? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            var entry = ProcessHistory.Create(
                entityType, entityId, action,
                oldValue, newValue, description,
                performedBy, performedByRole, metadata);

            _context.ProcessHistories.Add(entry);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "📋 ProcessHistory: {EntityType} {EntityId} → {Action} ({OldValue} → {NewValue}) | {Description}",
                entityType, entityId, action, oldValue ?? "-", newValue ?? "-", description);
        }
        catch (Exception ex)
        {
            // Process history logging should never break the main flow
            _logger.LogError(ex,
                "❌ Failed to log ProcessHistory: {EntityType} {EntityId} {Action}",
                entityType, entityId, action);
        }
    }
}
