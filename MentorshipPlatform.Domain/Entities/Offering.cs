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

    private Offering() { }

    public static Offering Create(
        Guid mentorUserId,
        OfferingType type,
        string title,
        int durationMin,
        decimal price)
    {
        return new Offering
        {
            MentorUserId = mentorUserId,
            Type = type,
            Title = title,
            DurationMinDefault = durationMin,
            PriceAmount = price,
            IsActive = true
        };
    }

    public void UpdatePrice(decimal newPrice)
    {
        PriceAmount = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }
    public void UpdateCurrency(string currency)
    {
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}