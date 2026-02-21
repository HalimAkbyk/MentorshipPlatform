using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Bookings.Commands.RejectReschedule;

public record RejectRescheduleCommand(Guid BookingId) : IRequest<Result>;

public class RejectRescheduleCommandHandler : IRequestHandler<RejectRescheduleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IEmailService _emailService;
    private readonly ILogger<RejectRescheduleCommandHandler> _logger;

    public RejectRescheduleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IEmailService emailService,
        ILogger<RejectRescheduleCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result> Handle(RejectRescheduleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        // Yalnizca student reddedebilir
        if (booking.StudentUserId != userId)
            return Result.Failure("Yalnizca ogrenci reschedule talebini reddedebilir");

        if (!booking.PendingRescheduleStartAt.HasValue)
            return Result.Failure("Reddedilecek bir reschedule talebi yok");

        var pendingStartAt = booking.PendingRescheduleStartAt.Value;
        booking.RejectReschedule();

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "RescheduleRejected",
            null, null,
            $"Ogrenci mentor'un reschedule talebini reddetti. Talep edilen saat: {pendingStartAt:yyyy-MM-dd HH:mm}",
            userId, "Student", ct: cancellationToken);

        // Notify mentor that reschedule was rejected
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.RescheduleRejected,
                booking.Mentor.Email!,
                new Dictionary<string, string>
                {
                    ["otherPartyName"] = booking.Student.DisplayName
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send reschedule rejected email for {BookingId}", booking.Id);
        }

        return Result.Success();
    }
}
