using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class Curriculum : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Subject { get; private set; }
    public string? Level { get; private set; }
    public int TotalWeeks { get; private set; }
    public int? EstimatedHoursPerWeek { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public CurriculumStatus Status { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsTemplate { get; private set; }
    public string? TemplateName { get; private set; }

    // Navigation
    public User Mentor { get; private set; } = null!;
    public ICollection<CurriculumWeek> Weeks { get; private set; } = new List<CurriculumWeek>();

    private Curriculum() { }

    public static Curriculum Create(
        Guid mentorUserId,
        string title,
        string? description = null,
        string? subject = null,
        string? level = null,
        int totalWeeks = 1,
        int? estimatedHoursPerWeek = null,
        string? coverImageUrl = null,
        bool isDefault = false)
    {
        return new Curriculum
        {
            MentorUserId = mentorUserId,
            Title = title,
            Description = description,
            Subject = subject,
            Level = level,
            TotalWeeks = totalWeeks,
            EstimatedHoursPerWeek = estimatedHoursPerWeek,
            CoverImageUrl = coverImageUrl,
            Status = CurriculumStatus.Draft,
            IsDefault = isDefault
        };
    }

    public void Update(
        string title,
        string? description,
        string? subject,
        string? level,
        int totalWeeks,
        int? estimatedHoursPerWeek,
        string? coverImageUrl,
        bool isDefault)
    {
        Title = title;
        Description = description;
        Subject = subject;
        Level = level;
        TotalWeeks = totalWeeks;
        EstimatedHoursPerWeek = estimatedHoursPerWeek;
        CoverImageUrl = coverImageUrl;
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = CurriculumStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = CurriculumStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAsTemplate(string templateName)
    {
        IsTemplate = true;
        TemplateName = templateName;
        UpdatedAt = DateTime.UtcNow;
    }

    public Curriculum DeepCopyFromTemplate(Guid mentorUserId, string? newTitle)
    {
        return new Curriculum
        {
            MentorUserId = mentorUserId,
            Title = newTitle ?? Title,
            Description = Description,
            Subject = Subject,
            Level = Level,
            TotalWeeks = TotalWeeks,
            EstimatedHoursPerWeek = EstimatedHoursPerWeek,
            CoverImageUrl = CoverImageUrl,
            Status = CurriculumStatus.Draft,
            IsDefault = false,
            IsTemplate = false
        };
    }
}
