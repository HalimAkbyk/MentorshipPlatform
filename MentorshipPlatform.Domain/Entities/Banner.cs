using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Banner : BaseEntity
{
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? LinkUrl { get; private set; }
    public string Position { get; private set; } = "Top"; // Top, Middle, Bottom
    public bool IsActive { get; private set; } = true;
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public int SortOrder { get; private set; }

    private Banner() { }

    public static Banner Create(string title, string? description, string? imageUrl, string? linkUrl, string position, DateTime? startDate, DateTime? endDate, int sortOrder)
    {
        return new Banner
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            ImageUrl = imageUrl,
            LinkUrl = linkUrl,
            Position = position,
            StartDate = startDate,
            EndDate = endDate,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string? description, string? imageUrl, string? linkUrl, string position, DateTime? startDate, DateTime? endDate, int sortOrder, bool isActive)
    {
        Title = title;
        Description = description;
        ImageUrl = imageUrl;
        LinkUrl = linkUrl;
        Position = position;
        StartDate = startDate;
        EndDate = endDate;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
