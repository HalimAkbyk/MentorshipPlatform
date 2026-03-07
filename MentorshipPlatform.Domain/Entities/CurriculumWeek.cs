using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class CurriculumWeek : BaseEntity
{
    public Guid CurriculumId { get; private set; }
    public int WeekNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }

    // Navigation
    public Curriculum Curriculum { get; private set; } = null!;
    public ICollection<CurriculumTopic> Topics { get; private set; } = new List<CurriculumTopic>();

    private CurriculumWeek() { }

    public static CurriculumWeek Create(
        Guid curriculumId,
        int weekNumber,
        string title,
        string? description = null,
        int sortOrder = 0)
    {
        return new CurriculumWeek
        {
            CurriculumId = curriculumId,
            WeekNumber = weekNumber,
            Title = title,
            Description = description,
            SortOrder = sortOrder
        };
    }

    public void Update(string title, string? description)
    {
        Title = title;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
}
