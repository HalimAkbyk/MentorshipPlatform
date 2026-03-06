using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Credits.Queries.GetMyCredits;

public record StudentCreditDto(
    Guid Id,
    string CreditType,
    int TotalCredits,
    int UsedCredits,
    int RemainingCredits,
    DateTime? ExpiresAt,
    Guid PackagePurchaseId,
    DateTime CreatedAt);

public record CreditSummaryDto(
    string CreditType,
    int TotalCredits,
    int UsedCredits,
    int RemainingCredits,
    List<StudentCreditDto> Details);

public record GetMyCreditsQuery() : IRequest<Result<List<CreditSummaryDto>>>;

public class GetMyCreditsQueryHandler
    : IRequestHandler<GetMyCreditsQuery, Result<List<CreditSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCreditsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CreditSummaryDto>>> Handle(
        GetMyCreditsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<CreditSummaryDto>>.Failure("Kullanıcı doğrulanamadı.");

        var studentId = _currentUser.UserId.Value;
        var now = DateTime.UtcNow;

        var credits = await _context.StudentCredits
            .AsNoTracking()
            .Where(c => c.StudentId == studentId
                && (c.TotalCredits - c.UsedCredits) > 0
                && (!c.ExpiresAt.HasValue || c.ExpiresAt.Value > now))
            .OrderBy(c => c.CreditType)
            .ThenBy(c => c.ExpiresAt)
            .ToListAsync(cancellationToken);

        var grouped = credits
            .GroupBy(c => c.CreditType)
            .Select(g => new CreditSummaryDto(
                g.Key.ToString(),
                g.Sum(c => c.TotalCredits),
                g.Sum(c => c.UsedCredits),
                g.Sum(c => c.RemainingCredits),
                g.Select(c => new StudentCreditDto(
                    c.Id,
                    c.CreditType.ToString(),
                    c.TotalCredits,
                    c.UsedCredits,
                    c.RemainingCredits,
                    c.ExpiresAt,
                    c.PackagePurchaseId,
                    c.CreatedAt)).ToList()))
            .ToList();

        return Result<List<CreditSummaryDto>>.Success(grouped);
    }
}
