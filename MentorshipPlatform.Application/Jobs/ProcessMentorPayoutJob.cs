using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Jobs;

public class ProcessMentorPayoutJob
{
    private readonly IApplicationDbContext _context;

    public ProcessMentorPayoutJob(IApplicationDbContext context)
    {
        _context = context;
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
    }
}