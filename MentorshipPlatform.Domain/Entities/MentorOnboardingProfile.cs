using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class MentorOnboardingProfile : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string? MentorType { get; private set; }
    public string? City { get; private set; }
    public string? Timezone { get; private set; }
    public string? Languages { get; private set; }
    public string? Categories { get; private set; }
    public string? Subtopics { get; private set; }
    public string? TargetAudience { get; private set; }
    public string? ExperienceLevels { get; private set; }
    public string? YearsOfExperience { get; private set; }
    public string? CurrentRole { get; private set; }
    public string? CurrentCompany { get; private set; }
    public string? PreviousCompanies { get; private set; }
    public string? Education { get; private set; }
    public string? Certifications { get; private set; }
    public string? LinkedinUrl { get; private set; }
    public string? GithubUrl { get; private set; }
    public string? PortfolioUrl { get; private set; }
    public string? YksExamType { get; private set; }
    public string? YksScore { get; private set; }
    public string? YksRanking { get; private set; }
    public string? MentoringTypes { get; private set; }
    public string? SessionFormats { get; private set; }
    public bool OfferFreeIntro { get; private set; } = true;

    private MentorOnboardingProfile() { }

    public static MentorOnboardingProfile Create(Guid mentorUserId)
    {
        return new MentorOnboardingProfile
        {
            MentorUserId = mentorUserId
        };
    }

    public void Update(
        string? mentorType,
        string? city,
        string? timezone,
        string? languages,
        string? categories,
        string? subtopics,
        string? targetAudience,
        string? experienceLevels,
        string? yearsOfExperience,
        string? currentRole,
        string? currentCompany,
        string? previousCompanies,
        string? education,
        string? certifications,
        string? linkedinUrl,
        string? githubUrl,
        string? portfolioUrl,
        string? yksExamType,
        string? yksScore,
        string? yksRanking,
        string? mentoringTypes,
        string? sessionFormats,
        bool offerFreeIntro)
    {
        MentorType = mentorType;
        City = city;
        Timezone = timezone;
        Languages = languages;
        Categories = categories;
        Subtopics = subtopics;
        TargetAudience = targetAudience;
        ExperienceLevels = experienceLevels;
        YearsOfExperience = yearsOfExperience;
        CurrentRole = currentRole;
        CurrentCompany = currentCompany;
        PreviousCompanies = previousCompanies;
        Education = education;
        Certifications = certifications;
        LinkedinUrl = linkedinUrl;
        GithubUrl = githubUrl;
        PortfolioUrl = portfolioUrl;
        YksExamType = yksExamType;
        YksScore = yksScore;
        YksRanking = yksRanking;
        MentoringTypes = mentoringTypes;
        SessionFormats = sessionFormats;
        OfferFreeIntro = offerFreeIntro;
        UpdatedAt = DateTime.UtcNow;
    }
}
