using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetPlatformTransactions;

public record PlatformTransactionDto(
    Guid Id,
    string AccountType,
    string Direction,
    decimal Amount,
    string Currency,
    string ReferenceType,
    Guid ReferenceId,
    DateTime CreatedAt,
    Guid? AccountOwnerUserId,
    string? AccountOwnerName);

public record GetPlatformTransactionsQuery(
    string? AccountType = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedList<PlatformTransactionDto>>>;

public class GetPlatformTransactionsQueryHandler
    : IRequestHandler<GetPlatformTransactionsQuery, Result<PaginatedList<PlatformTransactionDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPlatformTransactionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<PlatformTransactionDto>>> Handle(
        GetPlatformTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.LedgerEntries.AsNoTracking().AsQueryable();

        // Filter by account type
        if (!string.IsNullOrWhiteSpace(request.AccountType)
            && Enum.TryParse<LedgerAccountType>(request.AccountType, true, out var accountType))
        {
            query = query.Where(l => l.AccountType == accountType);
        }

        var orderedQuery = query.OrderByDescending(l => l.CreatedAt);

        var paginated = await orderedQuery
            .ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Get owner names
        var ownerIds = paginated.Items
            .Where(l => l.AccountOwnerUserId.HasValue)
            .Select(l => l.AccountOwnerUserId!.Value)
            .Distinct()
            .ToList();

        var owners = await _context.Users
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var dtos = paginated.Items.Select(l =>
        {
            string? ownerName = null;
            if (l.AccountOwnerUserId.HasValue)
                owners.TryGetValue(l.AccountOwnerUserId.Value, out ownerName);

            return new PlatformTransactionDto(
                l.Id,
                l.AccountType.ToString(),
                l.Direction.ToString(),
                l.Amount,
                l.Currency,
                l.ReferenceType,
                l.ReferenceId,
                l.CreatedAt,
                l.AccountOwnerUserId,
                ownerName);
        }).ToList();

        return Result<PaginatedList<PlatformTransactionDto>>.Success(
            new PaginatedList<PlatformTransactionDto>(
                dtos,
                paginated.TotalCount,
                paginated.PageNumber,
                paginated.PageSize));
    }
}
