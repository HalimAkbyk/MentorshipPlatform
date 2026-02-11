using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class MentorProfile : BaseEntity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    
    public string Bio { get; private set; } = string.Empty;
    public string University { get; private set; } = string.Empty;
    public string Department { get; private set; } = string.Empty;
    public int? GraduationYear { get; private set; }
    public string? Headline { get; private set; }
    
    public decimal RatingAvg { get; private set; }
    public int RatingCount { get; private set; }
    public bool IsListed { get; private set; }

    private readonly List<MentorVerification> _verifications = new();
    public IReadOnlyCollection<MentorVerification> Verifications => _verifications.AsReadOnly();

    private readonly List<Offering> _offerings = new();
    public IReadOnlyCollection<Offering> Offerings => _offerings.AsReadOnly();

    private MentorProfile() { }

    public static MentorProfile Create(Guid userId, string university, string department)
    {
        return new MentorProfile
        {
            UserId = userId,
            University = university,
            Department = department,
            IsListed = false
        };
    }

    public void UpdateProfile(string bio, string? headline, int? graduationYear)
    {
        Bio = bio;
        Headline = headline;
        GraduationYear = graduationYear;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRating(decimal newRating)
    {
        RatingAvg = ((RatingAvg * RatingCount) + newRating) / (RatingCount + 1);
        RatingCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish() => IsListed = true;
    public void Unpublish() => IsListed = false;

    // ✅ Mentor'un booking kabul edebilmesi için en az bir doğrulama onaylı olmalı
    public bool IsApprovedForBookings()
    {
        return _verifications.Any(v => v.Status == VerificationStatus.Approved);
    }

    public bool IsVerified(VerificationType type)
    {
        return _verifications.Any(v => 
            v.Type == type && 
            v.Status == VerificationStatus.Approved);
    }
}