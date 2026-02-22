using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Payouts.Commands.CreatePayoutRequest;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Payouts.Queries.GetMyPayoutRequests;

public record GetMyPayoutRequestsQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PayoutRequestListDto>>;

public record PayoutRequestListDto(
    List<PayoutRequestDto> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages);

public class GetMyPayoutRequestsQueryHandler
    : IRequestHandler<GetMyPayoutRequestsQuery, Result<PayoutRequestListDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyPayoutRequestsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PayoutRequestListDto>> Handle(
        GetMyPayoutRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PayoutRequestListDto>.Failure("Kullanıcı doğrulanmadı");

        var userId = _currentUser.UserId.Value;

        var query = _context.PayoutRequests
            .Where(p => p.MentorUserId == userId)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PayoutRequestDto(
                p.Id,
                p.Amount,
                p.Currency,
                p.Status.ToString(),
                p.MentorNote,
                p.AdminNote,
                p.CreatedAt,
                p.ProcessedAt))
            .ToListAsync(cancellationToken);

        return Result<PayoutRequestListDto>.Success(new PayoutRequestListDto(
            items, totalCount, request.Page, totalPages));
    }
}
