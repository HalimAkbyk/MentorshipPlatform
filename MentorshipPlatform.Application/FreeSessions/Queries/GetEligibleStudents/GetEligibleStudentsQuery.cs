using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.FreeSessions.Queries.GetEligibleStudents;

public record EligibleStudentDto(
    Guid StudentId,
    string DisplayName,
    string? AvatarUrl,
    int RemainingCredits);

public record GetEligibleStudentsQuery : IRequest<Result<List<EligibleStudentDto>>>;

public class GetEligibleStudentsQueryHandler
    : IRequestHandler<GetEligibleStudentsQuery, Result<List<EligibleStudentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetEligibleStudentsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<EligibleStudentDto>>> Handle(
        GetEligibleStudentsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Mentor))
            return Result<List<EligibleStudentDto>>.Failure("Yetkiniz yok");

        var students = await _context.StudentCredits
            .AsNoTracking()
            .Where(c => c.CreditType == CreditType.PrivateLesson
                     && c.UsedCredits < c.TotalCredits
                     && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .GroupBy(c => new { c.StudentId, c.Student.DisplayName, c.Student.AvatarUrl })
            .Select(g => new EligibleStudentDto(
                g.Key.StudentId,
                g.Key.DisplayName,
                g.Key.AvatarUrl,
                g.Sum(c => c.TotalCredits - c.UsedCredits)))
            .OrderBy(s => s.DisplayName)
            .ToListAsync(cancellationToken);

        return Result<List<EligibleStudentDto>>.Success(students);
    }
}
