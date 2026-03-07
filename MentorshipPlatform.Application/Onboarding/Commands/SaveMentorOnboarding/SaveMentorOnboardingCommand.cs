using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Onboarding.Commands.SaveMentorOnboarding;

public record SaveMentorOnboardingCommand(
    string? MentorType,
    string? City,
    string? Timezone,
    string? Languages,
    string? Categories,
    string? Subtopics,
    string? TargetAudience,
    string? ExperienceLevels,
    string? YearsOfExperience,
    string? CurrentRole,
    string? CurrentCompany,
    string? PreviousCompanies,
    string? Education,
    string? Certifications,
    string? LinkedinUrl,
    string? GithubUrl,
    string? PortfolioUrl,
    string? YksExamType,
    string? YksScore,
    string? YksRanking,
    string? MentoringTypes,
    string? SessionFormats,
    bool OfferFreeIntro = true) : IRequest<Result<MentorOnboardingDto>>;

public record MentorOnboardingDto(
    Guid Id,
    string? MentorType,
    string? City,
    string? Timezone,
    string? Languages,
    string? Categories,
    string? Subtopics,
    string? TargetAudience,
    string? ExperienceLevels,
    string? YearsOfExperience,
    string? CurrentRole,
    string? CurrentCompany,
    string? PreviousCompanies,
    string? Education,
    string? Certifications,
    string? LinkedinUrl,
    string? GithubUrl,
    string? PortfolioUrl,
    string? YksExamType,
    string? YksScore,
    string? YksRanking,
    string? MentoringTypes,
    string? SessionFormats,
    bool OfferFreeIntro);

public class SaveMentorOnboardingCommandValidator : AbstractValidator<SaveMentorOnboardingCommand>
{
    public SaveMentorOnboardingCommandValidator()
    {
        RuleFor(x => x.MentorType).MaximumLength(50).When(x => x.MentorType != null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City != null);
        RuleFor(x => x.Timezone).MaximumLength(100).When(x => x.Timezone != null);
        RuleFor(x => x.Categories).MaximumLength(1000).When(x => x.Categories != null);
        RuleFor(x => x.LinkedinUrl).MaximumLength(500).When(x => x.LinkedinUrl != null);
        RuleFor(x => x.GithubUrl).MaximumLength(500).When(x => x.GithubUrl != null);
        RuleFor(x => x.PortfolioUrl).MaximumLength(500).When(x => x.PortfolioUrl != null);
    }
}

public class SaveMentorOnboardingCommandHandler
    : IRequestHandler<SaveMentorOnboardingCommand, Result<MentorOnboardingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveMentorOnboardingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MentorOnboardingDto>> Handle(
        SaveMentorOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<MentorOnboardingDto>.Failure("Kullanıcı doğrulanmadı");

        var userId = _currentUser.UserId.Value;

        var profile = await _context.MentorOnboardingProfiles
            .FirstOrDefaultAsync(p => p.MentorUserId == userId, cancellationToken);

        if (profile == null)
        {
            profile = MentorOnboardingProfile.Create(userId);
            _context.MentorOnboardingProfiles.Add(profile);
        }

        profile.Update(
            request.MentorType,
            request.City,
            request.Timezone,
            request.Languages,
            request.Categories,
            request.Subtopics,
            request.TargetAudience,
            request.ExperienceLevels,
            request.YearsOfExperience,
            request.CurrentRole,
            request.CurrentCompany,
            request.PreviousCompanies,
            request.Education,
            request.Certifications,
            request.LinkedinUrl,
            request.GithubUrl,
            request.PortfolioUrl,
            request.YksExamType,
            request.YksScore,
            request.YksRanking,
            request.MentoringTypes,
            request.SessionFormats,
            request.OfferFreeIntro);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<MentorOnboardingDto>.Success(new MentorOnboardingDto(
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
