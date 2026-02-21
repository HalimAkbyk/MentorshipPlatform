using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class ProcessMentorPayoutJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessMentorPayoutJob> _logger;

    public ProcessMentorPayoutJob(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<ProcessMentorPayoutJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Execute(Guid bookingId)
    {
        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking == null || booking.Status != BookingStatus.Completed)
            return;

        // Move funds from escrow to available
        var escrowEntry = await _context.LedgerEntries
            .FirstOrDefaultAsync(l =>
                l.AccountType == LedgerAccountType.MentorEscrow &&
                l.AccountOwnerUserId == booking.MentorUserId &&
                l.ReferenceId == bookingId);

        if (escrowEntry == null) return;

        // Debit escrow
        _context.LedgerEntries.Add(LedgerEntry.Create(
            LedgerAccountType.MentorEscrow,
            LedgerDirection.Debit,
            escrowEntry.Amount,
            "Booking",
            bookingId,
            booking.MentorUserId));

        // Credit available
        _context.LedgerEntries.Add(LedgerEntry.Create(
            LedgerAccountType.MentorAvailable,
            LedgerDirection.Credit,
            escrowEntry.Amount,
            "Booking",
            bookingId,
            booking.MentorUserId));

        await _context.SaveChangesAsync();

        // Send payout processed email to mentor
        try
        {
            var mentorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == booking.MentorUserId);

            if (mentorUser?.Email != null)
            {
                var trCulture = new System.Globalization.CultureInfo("tr-TR");
                await _emailService.SendTemplatedEmailAsync(
                    EmailTemplateKeys.PayoutProcessed,
                    mentorUser.Email,
                    new Dictionary<string, string>
                    {
                        ["amount"] = escrowEntry.Amount.ToString("N2", trCulture),
                        ["paymentDate"] = DateTime.UtcNow.ToString("dd MMMM yyyy", trCulture)
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send payout email to mentor for booking {BookingId}", bookingId);
        }
    }
}