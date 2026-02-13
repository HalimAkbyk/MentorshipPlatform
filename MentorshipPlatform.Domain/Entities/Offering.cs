using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class Offering : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public OfferingType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DurationMinDefault { get; private set; }
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public bool IsActive { get; private set; }
    public string? MetadataJson { get; private set; }

    // Yeni alanlar - Paket zenginleştirme
    public string? Category { get; private set; }
    public string? Subtitle { get; private set; }
    public string? DetailedDescription { get; private set; }
    public string? SessionType { get; private set; }
    public int MaxBookingDaysAhead { get; private set; } = 60;
    public int MinNoticeHours { get; private set; } = 2;
    public int SortOrder { get; private set; }
    public string? CoverImageUrl { get; private set; }

    // Navigation properties
    private readonly List<BookingQuestion> _questions = new();
    public IReadOnlyCollection<BookingQuestion> Questions => _questions.AsReadOnly();

    private Offering() { }

    public static Offering Create(
        Guid mentorUserId,
        OfferingType type,
        string title,
        int durationMin,
        decimal price,
        string? description = null,
        string? category = null,
        string? subtitle = null,
        string? detailedDescription = null,
        string? sessionType = null)
    {
        return new Offering
        {
            MentorUserId = mentorUserId,
            Type = type,
            Title = title,
            DurationMinDefault = durationMin,
            PriceAmount = price,
            Description = description,
            Category = category,
            Subtitle = subtitle,
            DetailedDescription = detailedDescription,
            SessionType = sessionType,
            IsActive = true
        };
    }

    public void Update(
        string title,
        string? description,
        int durationMin,
        decimal price,
        string? category,
        string? subtitle,
        string? detailedDescription,
        string? sessionType,
        int maxBookingDaysAhead,
        int minNoticeHours,
        string? coverImageUrl)
    {
        Title = title;
        Description = description;
        if (durationMin > 0) DurationMinDefault = durationMin;
        if (price >= 0) PriceAmount = price;
        Category = category;
        Subtitle = subtitle;
        DetailedDescription = detailedDescription;
        SessionType = sessionType;
        MaxBookingDaysAhead = maxBookingDaysAhead > 0 ? maxBookingDaysAhead : 60;
        MinNoticeHours = minNoticeHours >= 0 ? minNoticeHours : 2;
        CoverImageUrl = coverImageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal newPrice)
    {
        PriceAmount = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDuration(int durationMin)
    {
        if (durationMin > 0)
        {
            DurationMinDefault = durationMin;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void UpdateCurrency(string currency)
    {
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSortOrder(int order)
    {
        SortOrder = order;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
