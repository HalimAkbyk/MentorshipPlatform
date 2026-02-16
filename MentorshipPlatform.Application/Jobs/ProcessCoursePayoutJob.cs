using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Moves course payment funds from MentorEscrow → MentorAvailable
/// after the 7-day refund window has passed.
/// Scheduled via Hangfire when a course payment is processed.
/// </summary>
public class ProcessCoursePayoutJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProcessCoursePayoutJob> _logger;

    public ProcessCoursePayoutJob(
        IApplicationDbContext context,
        ILogger<ProcessCoursePayoutJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Execute(Guid orderId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            _logger.LogWarning("ProcessCoursePayoutJob: Order {OrderId} not found", orderId);
            return;
        }

        // Only process paid course orders (skip if already refunded)
        if (order.Type != OrderType.Course ||
            (order.Status != OrderStatus.Paid && order.Status != OrderStatus.PartiallyRefunded))
        {
            _logger.LogInformation(
                "ProcessCoursePayoutJob: Skipping Order {OrderId} — Type={Type}, Status={Status}",
                orderId, order.Type, order.Status);
            return;
        }

        // Find the course enrollment to get mentor userId
        var enrollment = await _context.CourseEnrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == order.ResourceId);

        if (enrollment == null)
        {
            _logger.LogWarning("ProcessCoursePayoutJob: CourseEnrollment {ResourceId} not found", order.ResourceId);
            return;
        }

        var mentorUserId = enrollment.Course.MentorUserId;

        // Find the original escrow credit entry for this order
        var escrowEntry = await _context.LedgerEntries
            .FirstOrDefaultAsync(l =>
                l.AccountType == LedgerAccountType.MentorEscrow &&
                l.Direction == LedgerDirection.Credit &&
                l.AccountOwnerUserId == mentorUserId &&
                l.ReferenceId == orderId);

        if (escrowEntry == null)
        {
            _logger.LogWarning(
                "ProcessCoursePayoutJob: No escrow credit entry found for Order {OrderId}", orderId);
            return;
        }

        // Check if already paid out (debit entry exists)
        var alreadyPaidOut = await _context.LedgerEntries
            .AnyAsync(l =>
                l.AccountType == LedgerAccountType.MentorEscrow &&
                l.Direction == LedgerDirection.Debit &&
                l.AccountOwnerUserId == mentorUserId &&
                l.ReferenceId == orderId &&
                l.ReferenceType == "CoursePayout");

        if (alreadyPaidOut)
        {
            _logger.LogInformation(
                "ProcessCoursePayoutJob: Order {OrderId} already paid out, skipping", orderId);
            return;
        }

        // Calculate remaining escrow after any partial refunds
        var escrowDebits = await _context.LedgerEntries
            .Where(l =>
                l.AccountType == LedgerAccountType.MentorEscrow &&
                l.Direction == LedgerDirection.Debit &&
                l.AccountOwnerUserId == mentorUserId &&
                l.ReferenceId == orderId)
            .SumAsync(l => l.Amount);

        var remainingEscrow = escrowEntry.Amount - escrowDebits;

        if (remainingEscrow <= 0)
        {
            _logger.LogInformation(
                "ProcessCoursePayoutJob: No remaining escrow for Order {OrderId} (fully refunded)", orderId);
            return;
        }

        // Debit escrow
        _context.LedgerEntries.Add(LedgerEntry.Create(
            LedgerAccountType.MentorEscrow,
            LedgerDirection.Debit,
            remainingEscrow,
            "CoursePayout",
            orderId,
            mentorUserId));

        // Credit available
        _context.LedgerEntries.Add(LedgerEntry.Create(
            LedgerAccountType.MentorAvailable,
            LedgerDirection.Credit,
            remainingEscrow,
            "CoursePayout",
            orderId,
            mentorUserId));

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "✅ ProcessCoursePayoutJob: Moved {Amount:F2} from escrow to available for Order {OrderId}, Mentor {MentorId}",
            remainingEscrow, orderId, mentorUserId);
    }
}
