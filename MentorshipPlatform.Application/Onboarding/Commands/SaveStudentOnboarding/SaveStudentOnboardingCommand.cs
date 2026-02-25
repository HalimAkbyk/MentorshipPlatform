using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Onboarding.Commands.SaveStudentOnboarding;

public record SaveStudentOnboardingCommand(
    string? BirthDay,
    string? BirthMonth,
    string? Phone,
    string? City,
    string? Gender,
    string? Status,
    string? StatusDetail,
    string? Goals,
    string? Categories,
    string? Subtopics,
    string? Level,
    string? Preferences,
    int? BudgetMin,
    int? BudgetMax,
    string? Availability,
    string? SessionFormats) : IRequest<Result<StudentOnboardingDto>>;

public record StudentOnboardingDto(
    Guid Id,
    string? BirthDay,
    string? BirthMonth,
    string? Phone,
    string? City,
    string? Gender,
    string? Status,
    string? StatusDetail,
    string? Goals,
    string? Categories,
    string? Subtopics,
    string? Level,
    string? Preferences,
    int? BudgetMin,
    int? BudgetMax,
    string? Availability,
    string? SessionFormats);

public class SaveStudentOnboardingCommandValidator : AbstractValidator<SaveStudentOnboardingCommand>
{
    public SaveStudentOnboardingCommandValidator()
    {
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City != null);
        RuleFor(x => x.Gender).MaximumLength(50).When(x => x.Gender != null);
        RuleFor(x => x.Status).MaximumLength(50).When(x => x.Status != null);
        RuleFor(x => x.Goals).MaximumLength(1000).When(x => x.Goals != null);
        RuleFor(x => x.Categories).MaximumLength(1000).When(x => x.Categories != null);
        RuleFor(x => x.Subtopics).MaximumLength(2000).When(x => x.Subtopics != null);
        RuleFor(x => x.Level).MaximumLength(50).When(x => x.Level != null);
    }
}

public class SaveStudentOnboardingCommandHandler
    : IRequestHandler<SaveStudentOnboardingCommand, Result<StudentOnboardingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveStudentOnboardingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<StudentOnboardingDto>> Handle(
        SaveStudentOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<StudentOnboardingDto>.Failure("Kullanıcı doğrulanmadı");

        var userId = _currentUser.UserId.Value;

        var profile = await _context.StudentOnboardingProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            profile = StudentOnboardingProfile.Create(userId);
            _context.StudentOnboardingProfiles.Add(profile);
        }

        profile.Update(
            request.BirthDay,
            request.BirthMonth,
            request.Phone,
            request.City,
            request.Gender,
            request.Status,
            request.StatusDetail,
            request.Goals,
            request.Categories,
            request.Subtopics,
            request.Level,
            request.Preferences,
            request.BudgetMin,
            request.BudgetMax,
            request.Availability,
            request.SessionFormats);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<StudentOnboardingDto>.Success(new StudentOnboardingDto(
            profile.Id,
            profile.BirthDay,
            profile.BirthMonth,
            profile.Phone,
            profile.City,
            profile.Gender,
            profile.Status,
            profile.StatusDetail,
            profile.Goals,
            profile.Categories,
            profile.Subtopics,
            profile.Level,
            profile.Preferences,
            profile.BudgetMin,
            profile.BudgetMax,
            profile.Availability,
            profile.SessionFormats));
    }
}
