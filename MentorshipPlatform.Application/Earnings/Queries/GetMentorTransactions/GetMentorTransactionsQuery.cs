using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Earnings.Queries.GetMentorTransactions;

public record MentorTransactionDto(
    Guid Id,
    string Type,
    string AccountType,
    string Direction,
    decimal Amount,
    string Currency,
    Guid ReferenceId,
    DateTime CreatedAt,
    string? Description);

public record GetMentorTransactionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Type = null,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<Result<PaginatedList<MentorTransactionDto>>>;

public class GetMentorTransactionsQueryHandler
    : IRequestHandler<GetMentorTransactionsQuery, Result<PaginatedList<MentorTransactionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorTransactionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<MentorTransactionDto>>> Handle(
        GetMentorTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<MentorTransactionDto>>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var query = _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountOwnerUserId == userId
                     && (l.AccountType == LedgerAccountType.MentorEscrow
                      || l.AccountType == LedgerAccountType.MentorAvailable
                      || l.AccountType == LedgerAccountType.MentorPayout));

        // Filter by type (ReferenceType)
        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(l => l.ReferenceType == request.Type);

        if (request.From.HasValue)
            query = query.Where(l => l.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(l => l.CreatedAt <= request.To.Value);

        // Get paginated results
        var orderedQuery = query.OrderByDescending(l => l.CreatedAt);

        var paginatedEntries = await orderedQuery
            .ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Enrich with descriptions by looking up related orders and bookings/courses
        var referenceIds = paginatedEntries.Items.Select(l => l.ReferenceId).Distinct().ToList();

        // Get orders
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => referenceIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        // Get resource IDs from orders
        var bookingIds = orders.Where(o => o.Type == OrderType.Booking).Select(o => o.ResourceId).ToList();
        var courseEnrollmentIds = orders.Where(o => o.Type == OrderType.Course).Select(o => o.ResourceId).ToList();

        // Get booking details
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Student)
            .Include(b => b.Offering)
            .Where(b => bookingIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

        // Get course enrollment details
        var courseEnrollments = await _context.CourseEnrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => courseEnrollmentIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        var dtos = paginatedEntries.Items.Select(l =>
        {
            var order = orders.FirstOrDefault(o => o.Id == l.ReferenceId);
            string? description = null;

            if (order != null)
            {
                if (order.Type == OrderType.Booking)
                {
                    var booking = bookings.FirstOrDefault(b => b.Id == order.ResourceId);
                    if (booking != null)
                    {
                        var studentName = booking.Student?.DisplayName ?? "Bilinmeyen";
                        var offeringTitle = booking.Offering?.Title ?? "Ders";
                        description = l.Direction == LedgerDirection.Credit
                            ? $"{offeringTitle} - {studentName}"
                            : l.ReferenceType == "Refund"
                                ? $"İade - {offeringTitle}"
                                : $"Ödeme - {offeringTitle}";
                    }
                }
                else if (order.Type == OrderType.Course)
                {
                    var enrollment = courseEnrollments.FirstOrDefault(e => e.Id == order.ResourceId);
                    if (enrollment != null)
                    {
                        var courseTitle = enrollment.Course?.Title ?? "Kurs";
                        description = l.Direction == LedgerDirection.Credit
                            ? $"Kurs satışı - {courseTitle}"
                            : $"İade - {courseTitle}";
                    }
                }
            }

            // Fallback descriptions based on account type transitions
            if (description == null)
            {
                description = (l.AccountType, l.Direction) switch
                {
                    (LedgerAccountType.MentorEscrow, LedgerDirection.Credit) => "Ödeme alındı (emanet)",
                    (LedgerAccountType.MentorEscrow, LedgerDirection.Debit) => "Emanetten çıkış",
                    (LedgerAccountType.MentorAvailable, LedgerDirection.Credit) => "Bakiye aktifleştirildi",
                    (LedgerAccountType.MentorAvailable, LedgerDirection.Debit) => "Ödeme çekildi",
                    (LedgerAccountType.MentorPayout, LedgerDirection.Credit) => "Ödeme gönderildi",
                    _ => "İşlem"
                };
            }

            return new MentorTransactionDto(
                l.Id,
                l.ReferenceType,
                l.AccountType.ToString(),
                l.Direction.ToString(),
                l.Amount,
                l.Currency,
                l.ReferenceId,
                l.CreatedAt,
                description);
        }).ToList();

        return Result<PaginatedList<MentorTransactionDto>>.Success(
            new PaginatedList<MentorTransactionDto>(
                dtos,
                paginatedEntries.TotalCount,
                paginatedEntries.PageNumber,
                paginatedEntries.PageSize));
    }
}
