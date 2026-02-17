using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class PlatformSetting : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Category { get; private set; } = "General";  // General, Fee, Registration, Maintenance
    public Guid? UpdatedByUserId { get; private set; }

    private PlatformSetting() { }

    public static PlatformSetting Create(string key, string value, string? description, string category)
    {
        return new PlatformSetting
        {
            Key = key,
            Value = value,
            Description = description,
            Category = category
        };
    }

    public void UpdateValue(string value, Guid updatedByUserId)
    {
        Value = value;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
