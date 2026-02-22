using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Payouts.Queries.GetPayoutSettings;

public record GetPayoutSettingsQuery : IRequest<Result<PayoutSettingsDto>>;

public record PayoutSettingsDto(
    decimal MinimumPayoutAmount,
    decimal AvailableBalance,
    bool HasPendingRequest,
    decimal? PendingRequestAmount);

public class GetPayoutSettingsQueryHandler
    : IRequestHandler<GetPayoutSettingsQuery, Result<PayoutSettingsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformSettingService _settings;

    public GetPayoutSettingsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPlatformSettingService settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<Result<PayoutSettingsDto>> Handle(
        GetPayoutSettingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PayoutSettingsDto>.Failure("Kullanıcı doğrulanmadı");

        var userId = _currentUser.UserId.Value;

        var minAmount = await _settings.GetDecimalAsync(
            PlatformSettings.MinimumPayoutAmount, 100m, cancellationToken);

        // Calculate available balance
        var credits = await _context.LedgerEntries
            .Where(l => l.AccountOwnerUserId == userId &&
                        l.AccountType == LedgerAccountType.MentorAvailable &&
                        l.Direction == LedgerDirection.Credit)
            .SumAsync(l => l.Amount, cancellationToken);

        var debits = await _context.LedgerEntries
            .Where(l => l.AccountOwnerUserId == userId &&
                        l.AccountType == LedgerAccountType.MentorAvailable &&
                        l.Direction == LedgerDirection.Debit)
            .SumAsync(l => l.Amount, cancellationToken);

        var availableBalance = credits - debits;

        // Check for pending request
        var pendingRequest = await _context.PayoutRequests
            .FirstOrDefaultAsync(p => p.MentorUserId == userId &&
                                      p.Status == PayoutRequestStatus.Pending,
                cancellationToken);

        return Result<PayoutSettingsDto>.Success(new PayoutSettingsDto(
            minAmount,
            availableBalance,
            pendingRequest != null,
            pendingRequest?.Amount));
    }
}
