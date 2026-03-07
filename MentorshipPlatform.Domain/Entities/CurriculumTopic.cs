using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class CurriculumTopic : BaseEntity
{
    public Guid CurriculumWeekId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public string? ObjectiveText { get; private set; }
    public Guid? LinkedExamId { get; private set; }
    public Guid? LinkedAssignmentId { get; private set; }

    // Navigation
    public CurriculumWeek Week { get; private set; } = null!;
    public ICollection<CurriculumTopicMaterial> Materials { get; private set; } = new List<CurriculumTopicMaterial>();

    private CurriculumTopic() { }

    public static CurriculumTopic Create(
        Guid curriculumWeekId,
        string title,
        string? description = null,
        int sortOrder = 0,
        int? estimatedMinutes = null,
        string? objectiveText = null,
        Guid? linkedExamId = null,
        Guid? linkedAssignmentId = null)
    {
        return new CurriculumTopic
        {
            CurriculumWeekId = curriculumWeekId,
            Title = title,
            Description = description,
            SortOrder = sortOrder,
            EstimatedMinutes = estimatedMinutes,
            ObjectiveText = objectiveText,
            LinkedExamId = linkedExamId,
            LinkedAssignmentId = linkedAssignmentId
        };
    }

    public void Update(
        string title,
        string? description,
        int? estimatedMinutes,
        string? objectiveText,
        Guid? linkedExamId,
        Guid? linkedAssignmentId)
    {
        Title = title;
        Description = description;
        EstimatedMinutes = estimatedMinutes;
        ObjectiveText = objectiveText;
        LinkedExamId = linkedExamId;
        LinkedAssignmentId = linkedAssignmentId;
        UpdatedAt = DateTime.UtcNow;
    }
}
