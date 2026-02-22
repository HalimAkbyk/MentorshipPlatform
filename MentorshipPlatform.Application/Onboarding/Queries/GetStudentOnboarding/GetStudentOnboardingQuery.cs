using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Onboarding.Commands.SaveStudentOnboarding;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Onboarding.Queries.GetStudentOnboarding;

public record GetStudentOnboardingQuery : IRequest<Result<StudentOnboardingDto?>>;

public class GetStudentOnboardingQueryHandler
    : IRequestHandler<GetStudentOnboardingQuery, Result<StudentOnboardingDto?>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentOnboardingQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<StudentOnboardingDto?>> Handle(
        GetStudentOnboardingQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<StudentOnboardingDto?>.Failure("Kullanıcı doğrulanmadı");

        var profile = await _context.StudentOnboardingProfiles
            .FirstOrDefaultAsync(p => p.UserId == _currentUser.UserId.Value, cancellationToken);

        if (profile == null)
            return Result<StudentOnboardingDto?>.Success(null);

        return Result<StudentOnboardingDto?>.Success(new StudentOnboardingDto(
            profile.Id,
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
