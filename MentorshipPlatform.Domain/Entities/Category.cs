using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string EntityType { get; private set; } = "General"; // General, GroupClass, Course, Offering

    private Category() { }

    public static Category Create(string name, string? icon, int sortOrder, string entityType = "General")
    {
        return new Category
        {
            Name = name,
            Icon = icon,
            SortOrder = sortOrder,
            EntityType = entityType,
            IsActive = true
        };
    }

    public void Update(string name, string? icon, int sortOrder, string entityType)
    {
        Name = name;
        Icon = icon;
        SortOrder = sortOrder;
        EntityType = entityType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
}
