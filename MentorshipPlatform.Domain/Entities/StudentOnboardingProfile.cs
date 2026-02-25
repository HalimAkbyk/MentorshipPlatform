using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class StudentOnboardingProfile : BaseEntity
{
    public Guid UserId { get; private set; }
    public string? BirthDay { get; private set; }
    public string? BirthMonth { get; private set; }
    public string? Phone { get; private set; }
    public string? City { get; private set; }
    public string? Gender { get; private set; }
    public string? Status { get; private set; }
    public string? StatusDetail { get; private set; }
    public string? Goals { get; private set; }
    public string? Categories { get; private set; }
    public string? Subtopics { get; private set; }
    public string? Level { get; private set; }
    public string? Preferences { get; private set; }
    public int? BudgetMin { get; private set; }
    public int? BudgetMax { get; private set; }
    public string? Availability { get; private set; }
    public string? SessionFormats { get; private set; }

    private StudentOnboardingProfile() { }

    public static StudentOnboardingProfile Create(Guid userId)
    {
        return new StudentOnboardingProfile
        {
            UserId = userId
        };
    }

    public void Update(
        string? birthDay,
        string? birthMonth,
        string? phone,
        string? city,
        string? gender,
        string? status,
        string? statusDetail,
        string? goals,
        string? categories,
        string? subtopics,
        string? level,
        string? preferences,
        int? budgetMin,
        int? budgetMax,
        string? availability,
        string? sessionFormats)
    {
        BirthDay = birthDay;
        BirthMonth = birthMonth;
        Phone = phone;
        City = city;
        Gender = gender;
        Status = status;
        StatusDetail = statusDetail;
        Goals = goals;
        Categories = categories;
        Subtopics = subtopics;
        Level = level;
        Preferences = preferences;
        BudgetMin = budgetMin;
        BudgetMax = budgetMax;
        Availability = availability;
        SessionFormats = sessionFormats;
        UpdatedAt = DateTime.UtcNow;
    }
}
