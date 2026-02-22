using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Payouts.Queries.GetAllPayoutRequests;

public record GetAllPayoutRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Search = null) : IRequest<Result<AdminPayoutRequestListDto>>;

public record AdminPayoutRequestDto(
    Guid Id,
    Guid MentorUserId,
    string MentorName,
    string? MentorEmail,
    decimal Amount,
    string Currency,
    string Status,
    string? MentorNote,
    string? AdminNote,
    string? ProcessedByName,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

public record AdminPayoutRequestListDto(
    List<AdminPayoutRequestDto> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages,
    int PendingCount,
    decimal PendingTotal);

public class GetAllPayoutRequestsQueryHandler
    : IRequestHandler<GetAllPayoutRequestsQuery, Result<AdminPayoutRequestListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllPayoutRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminPayoutRequestListDto>> Handle(
        GetAllPayoutRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.PayoutRequests.AsQueryable();

        // Filter by status
        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<PayoutRequestStatus>(request.Status, true, out var statusEnum))
        {
            query = query.Where(p => p.Status == statusEnum);
        }

        // Search by mentor name/email
        if (!string.IsNullOrEmpty(request.Search))
        {
            var search = request.Search.ToLower();
            var matchingUserIds = await _context.Users
                .Where(u => u.DisplayName.ToLower().Contains(search) ||
                            u.Email.ToLower().Contains(search))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(p => matchingUserIds.Contains(p.MentorUserId));
        }

        // Stats (before pagination)
        var pendingCount = await _context.PayoutRequests
            .CountAsync(p => p.Status == PayoutRequestStatus.Pending, cancellationToken);
        var pendingTotal = await _context.PayoutRequests
            .Where(p => p.Status == PayoutRequestStatus.Pending)
            .SumAsync(p => p.Amount, cancellationToken);

        var orderedQuery = query.OrderByDescending(p => p.CreatedAt);
        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var payoutRequests = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Resolve mentor names and processed-by names
        var userIds = payoutRequests
            .Select(p => p.MentorUserId)
            .Union(payoutRequests.Where(p => p.ProcessedByUserId.HasValue).Select(p => p.ProcessedByUserId!.Value))
            .Distinct()
            .ToList();

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = payoutRequests.Select(p =>
        {
            users.TryGetValue(p.MentorUserId, out var mentor);
            string? processedByName = null;
            if (p.ProcessedByUserId.HasValue && users.TryGetValue(p.ProcessedByUserId.Value, out var admin))
                processedByName = admin.DisplayName;

            return new AdminPayoutRequestDto(
                p.Id,
                p.MentorUserId,
                mentor?.DisplayName ?? "Bilinmiyor",
                mentor?.Email,
                p.Amount,
                p.Currency,
                p.Status.ToString(),
                p.MentorNote,
                p.AdminNote,
                processedByName,
                p.CreatedAt,
                p.ProcessedAt);
        }).ToList();

        return Result<AdminPayoutRequestListDto>.Success(new AdminPayoutRequestListDto(
            items, totalCount, request.Page, totalPages, pendingCount, pendingTotal));
    }
}
