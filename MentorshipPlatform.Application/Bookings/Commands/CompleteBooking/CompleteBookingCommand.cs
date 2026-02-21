using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Bookings.Commands.CompleteBooking;

public record CompleteBookingCommand(Guid BookingId) : IRequest<Result>;

public class CompleteBookingCommandHandler : IRequestHandler<CompleteBookingCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IEmailService _emailService;
    private readonly ILogger<CompleteBookingCommandHandler> _logger;

    public CompleteBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IEmailService emailService,
        ILogger<CompleteBookingCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result> Handle(CompleteBookingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        if (booking.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Unauthorized");

        // Seans süresi henüz dolmadıysa tamamlamaya izin verme
        if (DateTime.UtcNow < booking.EndAt)
        {
            await _history.LogAsync("Booking", booking.Id, "EarlyEndAttempt",
                booking.Status.ToString(), booking.Status.ToString(),
                $"Mentor seansı erken sonlandırmaya çalıştı. Planlanan bitiş: {booking.EndAt:HH:mm}, Şu an: {DateTime.UtcNow:HH:mm}",
                _currentUser.UserId.Value, "Mentor", ct: cancellationToken);

            return Result.Failure("Seans süresi henüz dolmadı. Video oturumu sonlandırıldı ancak seans devam ediyor. Odayı tekrar aktifleştirebilirsiniz.");
        }

        var oldStatus = booking.Status.ToString();
        booking.Complete();

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "StatusChanged",
            oldStatus, "Completed",
            "Mentor seansı tamamladı",
            _currentUser.UserId.Value, "Mentor", ct: cancellationToken);

        // Send completion email to student
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.BookingCompleted,
                booking.Student.Email!,
                new Dictionary<string, string>
                {
                    ["otherPartyName"] = booking.Mentor.DisplayName,
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking completion email for {BookingId}", booking.Id);
        }

        return Result.Success();
    }
}
