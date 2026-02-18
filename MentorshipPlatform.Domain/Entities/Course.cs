using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Domain.Entities;

public class Course : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ShortDescription { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? CoverImagePosition { get; private set; }
    public string? CoverImageTransform { get; private set; }
    public string? PromoVideoKey { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public CourseStatus Status { get; private set; }
    public CourseLevel Level { get; private set; }
    public string? Language { get; private set; } = "tr";
    public string? Category { get; private set; }

    // JSONB serialized string arrays
    public string? WhatYouWillLearnJson { get; private set; }
    public string? RequirementsJson { get; private set; }
    public string? TargetAudienceJson { get; private set; }

    // Denormalized stats
    public int TotalDurationSec { get; private set; }
    public int TotalLectures { get; private set; }
    public decimal RatingAvg { get; private set; }
    public int RatingCount { get; private set; }
    public int EnrollmentCount { get; private set; }

    // Navigation
    public User MentorUser { get; private set; } = null!;
    private readonly List<CourseSection> _sections = new();
    public IReadOnlyCollection<CourseSection> Sections => _sections.AsReadOnly();
    private readonly List<CourseReviewRound> _reviewRounds = new();
    public IReadOnlyCollection<CourseReviewRound> ReviewRounds => _reviewRounds.AsReadOnly();

    private Course() { }

    public static Course Create(
        Guid mentorUserId,
        string title,
        decimal price,
        string? shortDescription = null,
        string? description = null,
        string? category = null,
        string? language = null,
        CourseLevel level = CourseLevel.AllLevels)
    {
        return new Course
        {
            MentorUserId = mentorUserId,
            Title = title,
            Price = price,
            ShortDescription = shortDescription,
            Description = description,
            Category = category,
            Language = language ?? "tr",
            Level = level,
            Status = CourseStatus.Draft
        };
    }

    public void Update(
        string title,
        string? shortDescription,
        string? description,
        decimal price,
        string? category,
        string? language,
        CourseLevel level,
        string? coverImageUrl,
        string? coverImagePosition,
        string? coverImageTransform,
        string? promoVideoKey,
        string? whatYouWillLearnJson,
        string? requirementsJson,
        string? targetAudienceJson)
    {
        Title = title;
        ShortDescription = shortDescription;
        Description = description;
        if (price >= 0) Price = price;
        Category = category;
        Language = language;
        Level = level;
        CoverImageUrl = coverImageUrl;
        CoverImagePosition = coverImagePosition;
        CoverImageTransform = coverImageTransform;
        PromoVideoKey = promoVideoKey;
        WhatYouWillLearnJson = whatYouWillLearnJson;
        RequirementsJson = requirementsJson;
        TargetAudienceJson = targetAudienceJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        if (Status == CourseStatus.Published) return;
        Status = CourseStatus.Published;
        AddDomainEvent(new CoursePublishedEvent(Id, MentorUserId, Title));
    }

    public void SubmitForReview()
    {
        Status = CourseStatus.PendingReview;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApproveReview()
    {
        Status = CourseStatus.Published;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new CoursePublishedEvent(Id, MentorUserId, Title));
    }

    public void RejectReview()
    {
        Status = CourseStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequestRevision()
    {
        Status = CourseStatus.RevisionRequested;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResubmitForReview()
    {
        Status = CourseStatus.PendingReview;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = CourseStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        Status = CourseStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = CourseStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unsuspend()
    {
        Status = CourseStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStats(int totalDurationSec, int totalLectures)
    {
        TotalDurationSec = totalDurationSec;
        TotalLectures = totalLectures;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRating(decimal ratingAvg, int ratingCount)
    {
        RatingAvg = ratingAvg;
        RatingCount = ratingCount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementEnrollmentCount()
    {
        EnrollmentCount++;
        UpdatedAt = DateTime.UtcNow;
    }
}
