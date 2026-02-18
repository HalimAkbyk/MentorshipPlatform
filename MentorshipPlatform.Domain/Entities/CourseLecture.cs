using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class CourseLecture : BaseEntity
{
    public Guid SectionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? VideoKey { get; private set; }
    public int DurationSec { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPreview { get; private set; }
    public LectureType Type { get; private set; }
    public string? TextContent { get; private set; }

    // Navigation
    public CourseSection Section { get; private set; } = null!;

    private CourseLecture() { }

    public static CourseLecture Create(
        Guid sectionId,
        string title,
        LectureType type,
        int sortOrder,
        bool isPreview = false,
        string? description = null)
    {
        return new CourseLecture
        {
            SectionId = sectionId,
            Title = title,
            Type = type,
            SortOrder = sortOrder,
            IsPreview = isPreview,
            Description = description
        };
    }

    public void Update(string title, string? description, bool isPreview, string? textContent = null)
    {
        Title = title;
        Description = description;
        IsPreview = isPreview;
        if (Type == LectureType.Text)
            TextContent = textContent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetVideoKey(string videoKey, int durationSec)
    {
        VideoKey = videoKey;
        DurationSec = durationSec;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSortOrder(int order)
    {
        SortOrder = order;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReplaceVideo(string newVideoKey, int durationSec)
    {
        VideoKey = newVideoKey;
        DurationSec = durationSec;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPreview() => IsPreview = true;
    public void UnmarkAsPreview() => IsPreview = false;
}
