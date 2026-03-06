using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Credits.Queries.GetMyCreditTransactions;

public record CreditTransactionDto(
    Guid Id,
    string CreditType,
    string TransactionType,
    int Amount,
    string? Description,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    DateTime CreatedAt);

public record CreditTransactionPagedResult(
    List<CreditTransactionDto> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public record GetMyCreditTransactionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? CreditType = null) : IRequest<Result<CreditTransactionPagedResult>>;

public class GetMyCreditTransactionsQueryHandler
    : IRequestHandler<GetMyCreditTransactionsQuery, Result<CreditTransactionPagedResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCreditTransactionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CreditTransactionPagedResult>> Handle(
        GetMyCreditTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<CreditTransactionPagedResult>.Failure("Kullanıcı doğrulanamadı.");

        var studentId = _currentUser.UserId.Value;

        // Get the student's credit IDs first
        var creditQuery = _context.StudentCredits
            .Where(c => c.StudentId == studentId);

        if (!string.IsNullOrEmpty(request.CreditType) &&
            Enum.TryParse<CreditType>(request.CreditType, true, out var creditType))
        {
            creditQuery = creditQuery.Where(c => c.CreditType == creditType);
        }

        var creditIds = await creditQuery
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var transactionQuery = _context.CreditTransactions
            .AsNoTracking()
            .Include(t => t.StudentCredit)
            .Where(t => creditIds.Contains(t.StudentCreditId));

        var totalCount = await transactionQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var items = await transactionQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new CreditTransactionDto(
                t.Id,
                t.StudentCredit.CreditType.ToString(),
                t.TransactionType.ToString(),
                t.Amount,
                t.Description,
                t.RelatedEntityId,
                t.RelatedEntityType,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<CreditTransactionPagedResult>.Success(
            new CreditTransactionPagedResult(
                items,
                totalCount,
                request.Page,
                totalPages,
                request.Page > 1,
                request.Page < totalPages));
    }
}
