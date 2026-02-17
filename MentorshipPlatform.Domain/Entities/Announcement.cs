using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Announcement : BaseEntity
{
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public string Type { get; private set; } = "Info"; // Info, Warning, Maintenance
    public string TargetAudience { get; private set; } = "All"; // All, Students, Mentors
    public bool IsActive { get; private set; } = true;
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsDismissible { get; private set; } = true;

    private Announcement() { }

    public static Announcement Create(string title, string content, string type, string targetAudience, DateTime? startDate, DateTime? endDate, bool isDismissible)
    {
        return new Announcement
        {
            Id = Guid.NewGuid(),
            Title = title,
            Content = content,
            Type = type,
            TargetAudience = targetAudience,
            StartDate = startDate,
            EndDate = endDate,
            IsDismissible = isDismissible,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string content, string type, string targetAudience, DateTime? startDate, DateTime? endDate, bool isDismissible, bool isActive)
    {
        Title = title;
        Content = content;
        Type = type;
        TargetAudience = targetAudience;
        StartDate = startDate;
        EndDate = endDate;
        IsDismissible = isDismissible;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
