using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class FeatureFlag : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public string? Description { get; private set; }

    private FeatureFlag() { }

    public static FeatureFlag Create(string key, bool isEnabled, string? description)
    {
        return new FeatureFlag
        {
            Key = key,
            IsEnabled = isEnabled,
            Description = description
        };
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
