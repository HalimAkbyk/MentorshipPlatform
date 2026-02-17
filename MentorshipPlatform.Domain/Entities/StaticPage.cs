using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class StaticPage : BaseEntity
{
    public string Slug { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!; // HTML/Markdown
    public string? MetaTitle { get; private set; }
    public string? MetaDescription { get; private set; }
    public bool IsPublished { get; private set; }

    private StaticPage() { }

    public static StaticPage Create(string slug, string title, string content, string? metaTitle, string? metaDescription)
    {
        return new StaticPage
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = title,
            Content = content,
            MetaTitle = metaTitle,
            MetaDescription = metaDescription,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string content, string? metaTitle, string? metaDescription, bool isPublished)
    {
        Title = title;
        Content = content;
        MetaTitle = metaTitle;
        MetaDescription = metaDescription;
        IsPublished = isPublished;
        UpdatedAt = DateTime.UtcNow;
    }
}
