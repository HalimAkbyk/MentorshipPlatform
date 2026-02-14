using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class PresetAvatar : BaseEntity
{
    public string Url { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private PresetAvatar() { }

    public PresetAvatar(string url, string label, int sortOrder)
    {
        Url = url;
        Label = label;
        SortOrder = sortOrder;
    }

    public void Update(string url, string label, int sortOrder, bool isActive)
    {
        Url = url;
        Label = label;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
