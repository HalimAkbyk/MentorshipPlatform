using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class CourseSection : BaseEntity
{
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    // Navigation
    public Course Course { get; private set; } = null!;
    private readonly List<CourseLecture> _lectures = new();
    public IReadOnlyCollection<CourseLecture> Lectures => _lectures.AsReadOnly();

    private CourseSection() { }

    public static CourseSection Create(Guid courseId, string title, int sortOrder)
    {
        return new CourseSection
        {
            CourseId = courseId,
            Title = title,
            SortOrder = sortOrder
        };
    }

    public void Update(string title)
    {
        Title = title;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSortOrder(int order)
    {
        SortOrder = order;
        UpdatedAt = DateTime.UtcNow;
    }
}
