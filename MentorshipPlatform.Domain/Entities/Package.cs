using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Package : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int PrivateLessonCredits { get; private set; }
    public int GroupLessonCredits { get; private set; }
    public int VideoAccessCredits { get; private set; }
    public bool IsActive { get; private set; }
    public int? ValidityDays { get; private set; }
    public int SortOrder { get; private set; }

    private Package() { }

    public static Package Create(
        string name,
        string? description,
        decimal price,
        int privateLessonCredits,
        int groupLessonCredits,
        int videoAccessCredits,
        int? validityDays = null,
        int sortOrder = 0)
    {
        return new Package
        {
            Name = name,
            Description = description,
            Price = price,
            PrivateLessonCredits = privateLessonCredits,
            GroupLessonCredits = groupLessonCredits,
            VideoAccessCredits = videoAccessCredits,
            IsActive = true,
            ValidityDays = validityDays,
            SortOrder = sortOrder
        };
    }

    public void Update(
        string name,
        string? description,
        decimal price,
        int privateLessonCredits,
        int groupLessonCredits,
        int videoAccessCredits,
        int? validityDays,
        int sortOrder)
    {
        Name = name;
        Description = description;
        Price = price;
        PrivateLessonCredits = privateLessonCredits;
        GroupLessonCredits = groupLessonCredits;
        VideoAccessCredits = videoAccessCredits;
        ValidityDays = validityDays;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
