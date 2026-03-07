using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Onboarding.Commands.SaveMentorOnboarding;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Onboarding.Queries.GetMentorOnboarding;

public record GetMentorOnboardingQuery : IRequest<Result<MentorOnboardingDto?>>;

public class GetMentorOnboardingQueryHandler
    : IRequestHandler<GetMentorOnboardingQuery, Result<MentorOnboardingDto?>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorOnboardingQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MentorOnboardingDto?>> Handle(
        GetMentorOnboardingQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<MentorOnboardingDto?>.Failure("Kullanıcı doğrulanmadı");

        var profile = await _context.MentorOnboardingProfiles
            .FirstOrDefaultAsync(p => p.MentorUserId == _currentUser.UserId.Value, cancellationToken);

        if (profile == null)
            return Result<MentorOnboardingDto?>.Success(null);

        return Result<MentorOnboardingDto?>.Success(new MentorOnboardingDto(
            profile.Id,
            profile.MentorType,
            profile.City,
            profile.Timezone,
            profile.Languages,
            profile.Categories,
            profile.Subtopics,
            profile.TargetAudience,
            profile.ExperienceLevels,
            profile.YearsOfExperience,
            profile.CurrentRole,
            profile.CurrentCompany,
            profile.PreviousCompanies,
            profile.Education,
            profile.Certifications,
            profile.LinkedinUrl,
            profile.GithubUrl,
            profile.PortfolioUrl,
            profile.YksExamType,
            profile.YksScore,
            profile.YksRanking,
            profile.MentoringTypes,
            profile.SessionFormats,
            profile.OfferFreeIntro));
    }
}
