using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Payouts.Commands.CreatePayoutRequest;

public record CreatePayoutRequestCommand(
    decimal Amount,
    string? Note = null) : IRequest<Result<PayoutRequestDto>>;

public record PayoutRequestDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    string? MentorNote,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

public class CreatePayoutRequestCommandHandler
    : IRequestHandler<CreatePayoutRequestCommand, Result<PayoutRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformSettingService _settings;

    public CreatePayoutRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPlatformSettingService settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<Result<PayoutRequestDto>> Handle(
        CreatePayoutRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PayoutRequestDto>.Failure("Kullanıcı doğrulanmadı");

        var userId = _currentUser.UserId.Value;

        // Check minimum payout amount
        var minAmount = await _settings.GetDecimalAsync(
            PlatformSettings.MinimumPayoutAmount, 100m, cancellationToken);

        if (request.Amount < minAmount)
            return Result<PayoutRequestDto>.Failure(
                $"Minimum ödeme talep tutarı {minAmount:N2} TRY'dir");

        // Calculate available balance
        var mentorAvailableCredits = await _context.LedgerEntries
            .Where(l => l.AccountOwnerUserId == userId &&
                        l.AccountType == LedgerAccountType.MentorAvailable &&
                        l.Direction == LedgerDirection.Credit)
            .SumAsync(l => l.Amount, cancellationToken);

        var mentorAvailableDebits = await _context.LedgerEntries
            .Where(l => l.AccountOwnerUserId == userId &&
                        l.AccountType == LedgerAccountType.MentorAvailable &&
                        l.Direction == LedgerDirection.Debit)
            .SumAsync(l => l.Amount, cancellationToken);

        var availableBalance = mentorAvailableCredits - mentorAvailableDebits;

        if (request.Amount > availableBalance)
            return Result<PayoutRequestDto>.Failure(
                $"Yetersiz bakiye. Kullanılabilir bakiyeniz: {availableBalance:N2} TRY");

        // Check for existing pending request
        var hasPending = await _context.PayoutRequests
            .AnyAsync(p => p.MentorUserId == userId &&
                           p.Status == PayoutRequestStatus.Pending,
                cancellationToken);

        if (hasPending)
            return Result<PayoutRequestDto>.Failure(
                "Zaten bekleyen bir ödeme talebiniz var. Yeni talep oluşturmak için mevcut talebin sonuçlanmasını bekleyin.");

        // Create payout request
        var payoutRequest = PayoutRequest.Create(userId, request.Amount, request.Note);
        _context.PayoutRequests.Add(payoutRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<PayoutRequestDto>.Success(new PayoutRequestDto(
            payoutRequest.Id,
            payoutRequest.Amount,
            payoutRequest.Currency,
            payoutRequest.Status.ToString(),
            payoutRequest.MentorNote,
            payoutRequest.AdminNote,
            payoutRequest.CreatedAt,
            payoutRequest.ProcessedAt));
    }
}
