using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class HomepageModule : BaseEntity
{
    public string ModuleType { get; private set; } = null!; // HeroBanner, FeaturedMentors, PopularCourses, Testimonials, Categories, Stats, CTA
    public string Title { get; private set; } = null!;
    public string? Subtitle { get; private set; }
    public string? Content { get; private set; } // JSON content
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private HomepageModule() { }

    public static HomepageModule Create(string moduleType, string title, string? subtitle, string? content, int sortOrder)
    {
        return new HomepageModule
        {
            Id = Guid.NewGuid(),
            ModuleType = moduleType,
            Title = title,
            Subtitle = subtitle,
            Content = content,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string? subtitle, string? content, int sortOrder, bool isActive)
    {
        Title = title;
        Subtitle = subtitle;
        Content = content;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSortOrder(int sortOrder) { SortOrder = sortOrder; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
}
