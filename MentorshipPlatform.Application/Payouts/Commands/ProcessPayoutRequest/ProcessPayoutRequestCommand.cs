using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Payouts.Commands.ProcessPayoutRequest;

public record ProcessPayoutRequestCommand(
    Guid PayoutRequestId,
    string Action,          // "approve" or "reject"
    string? AdminNote = null) : IRequest<Result>;

public class ProcessPayoutRequestCommandHandler
    : IRequestHandler<ProcessPayoutRequestCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ProcessPayoutRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        ProcessPayoutRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("Kullanıcı doğrulanmadı");

        var adminUserId = _currentUser.UserId.Value;

        var payoutRequest = await _context.PayoutRequests
            .FirstOrDefaultAsync(p => p.Id == request.PayoutRequestId, cancellationToken);

        if (payoutRequest == null)
            return Result.Failure("Ödeme talebi bulunamadı");

        if (payoutRequest.Status != PayoutRequestStatus.Pending)
            return Result.Failure("Bu talep zaten işlenmiş");

        if (request.Action.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            payoutRequest.Reject(adminUserId, request.AdminNote);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        if (request.Action.Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            // Verify available balance is still sufficient
            var mentorAvailableCredits = await _context.LedgerEntries
                .Where(l => l.AccountOwnerUserId == payoutRequest.MentorUserId &&
                            l.AccountType == LedgerAccountType.MentorAvailable &&
                            l.Direction == LedgerDirection.Credit)
                .SumAsync(l => l.Amount, cancellationToken);

            var mentorAvailableDebits = await _context.LedgerEntries
                .Where(l => l.AccountOwnerUserId == payoutRequest.MentorUserId &&
                            l.AccountType == LedgerAccountType.MentorAvailable &&
                            l.Direction == LedgerDirection.Debit)
                .SumAsync(l => l.Amount, cancellationToken);

            var availableBalance = mentorAvailableCredits - mentorAvailableDebits;

            if (payoutRequest.Amount > availableBalance)
                return Result.Failure(
                    $"Mentor bakiyesi yetersiz. Mevcut bakiye: {availableBalance:N2} TRY, talep: {payoutRequest.Amount:N2} TRY");

            // Create ledger entries: Debit MentorAvailable, Credit MentorPayout
            var debitEntry = LedgerEntry.Create(
                LedgerAccountType.MentorAvailable,
                LedgerDirection.Debit,
                payoutRequest.Amount,
                "PayoutRequest",
                payoutRequest.Id,
                payoutRequest.MentorUserId);

            var creditEntry = LedgerEntry.Create(
                LedgerAccountType.MentorPayout,
                LedgerDirection.Credit,
                payoutRequest.Amount,
                "PayoutRequest",
                payoutRequest.Id,
                payoutRequest.MentorUserId);

            _context.LedgerEntries.Add(debitEntry);
            _context.LedgerEntries.Add(creditEntry);

            // Mark as completed (approve + complete in one step)
            payoutRequest.Complete(adminUserId, request.AdminNote);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        return Result.Failure("Geçersiz işlem. 'approve' veya 'reject' olmalıdır.");
    }
}
