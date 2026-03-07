using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class Assignment : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Instructions { get; private set; }
    public AssignmentType AssignmentType { get; private set; }
    public DifficultyLevel? DifficultyLevel { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public DateTime? DueDate { get; private set; }
    public int? MaxScore { get; private set; }
    public bool AllowLateSubmission { get; private set; }
    public int? LatePenaltyPercent { get; private set; }
    public Guid? BookingId { get; private set; }
    public Guid? GroupClassId { get; private set; }
    public Guid? CurriculumTopicId { get; private set; }
    public AssignmentStatus Status { get; private set; }

    // Navigation
    public User Mentor { get; private set; } = null!;
    public ICollection<AssignmentMaterial> Materials { get; private set; } = new List<AssignmentMaterial>();
    public ICollection<AssignmentSubmission> Submissions { get; private set; } = new List<AssignmentSubmission>();

    private Assignment() { }

    public static Assignment Create(
        Guid mentorUserId,
        string title,
        AssignmentType assignmentType,
        string? description = null,
        string? instructions = null,
        DifficultyLevel? difficultyLevel = null,
        int? estimatedMinutes = null,
        DateTime? dueDate = null,
        int? maxScore = null,
        bool allowLateSubmission = false,
        int? latePenaltyPercent = null,
        Guid? bookingId = null,
        Guid? groupClassId = null,
        Guid? curriculumTopicId = null)
    {
        return new Assignment
        {
            MentorUserId = mentorUserId,
            Title = title,
            AssignmentType = assignmentType,
            Description = description,
            Instructions = instructions,
            DifficultyLevel = difficultyLevel,
            EstimatedMinutes = estimatedMinutes,
            DueDate = dueDate,
            MaxScore = maxScore,
            AllowLateSubmission = allowLateSubmission,
            LatePenaltyPercent = latePenaltyPercent,
            BookingId = bookingId,
            GroupClassId = groupClassId,
            CurriculumTopicId = curriculumTopicId,
            Status = AssignmentStatus.Draft
        };
    }

    public void Update(
        string title,
        string? description,
        string? instructions,
        AssignmentType assignmentType,
        DifficultyLevel? difficultyLevel,
        int? estimatedMinutes,
        DateTime? dueDate,
        int? maxScore,
        bool allowLateSubmission,
        int? latePenaltyPercent,
        Guid? bookingId,
        Guid? groupClassId,
        Guid? curriculumTopicId)
    {
        Title = title;
        Description = description;
        Instructions = instructions;
        AssignmentType = assignmentType;
        DifficultyLevel = difficultyLevel;
        EstimatedMinutes = estimatedMinutes;
        DueDate = dueDate;
        MaxScore = maxScore;
        AllowLateSubmission = allowLateSubmission;
        LatePenaltyPercent = latePenaltyPercent;
        BookingId = bookingId;
        GroupClassId = groupClassId;
        CurriculumTopicId = curriculumTopicId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = AssignmentStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close()
    {
        Status = AssignmentStatus.Closed;
        UpdatedAt = DateTime.UtcNow;
    }
}
