using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class BlacklistEntry : BaseEntity
{
    public string Type { get; private set; } = string.Empty;     // Word, IP, Email
    public string Value { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private BlacklistEntry() { }

    public static BlacklistEntry Create(string type, string value, string? reason, Guid createdByUserId)
    {
        return new BlacklistEntry
        {
            Type = type,
            Value = value,
            Reason = reason,
            CreatedByUserId = createdByUserId
        };
    }

    public void Update(string value, string? reason)
    {
        Value = value;
        Reason = reason;
        UpdatedAt = DateTime.UtcNow;
    }
}
