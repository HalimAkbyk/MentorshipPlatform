using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Bookings.Commands.ReportNoShow;

/// <summary>
/// Mentor can manually report student no-show after StartAt + NoShowWaitMinutes.
/// Only the mentor of the booking can trigger this.
/// </summary>
public record ReportNoShowCommand(Guid BookingId) : IRequest<Result<string>>;

public class ReportNoShowCommandHandler : IRequestHandler<ReportNoShowCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlatformSettingService _settings;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<ReportNoShowCommandHandler> _logger;

    public ReportNoShowCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPlatformSettingService settings,
        IProcessHistoryService history,
        ILogger<ReportNoShowCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
        _history = history;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(ReportNoShowCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result<string>.Failure("Yetkilendirme hatası");

        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result<string>.Failure("Randevu bulunamadı");

        // Only the mentor of this booking can report no-show
        if (booking.MentorUserId != userId.Value)
            return Result<string>.Failure("Yalnızca mentor no-show raporu verebilir");

        if (booking.Status != BookingStatus.Confirmed)
            return Result<string>.Failure("Yalnızca onaylanmış randevular için no-show raporu verilebilir");

        // Check time: must be at least NoShowWaitMinutes after StartAt
        var noShowWait = await _settings.GetIntAsync(PlatformSettings.NoShowWaitMinutes, 15, cancellationToken);
        var devMode = await _settings.GetBoolAsync(PlatformSettings.DevModeSessionBypass, false, cancellationToken);

        if (!devMode)
        {
            var minutesSinceStart = (DateTime.UtcNow - booking.StartAt).TotalMinutes;
            if (minutesSinceStart < noShowWait)
                return Result<string>.Failure(
                    $"No-show raporu en erken ders başlangıcından {noShowWait} dakika sonra verilebilir. " +
                    $"Kalan süre: {Math.Ceiling(noShowWait - minutesSinceStart)} dakika.");
        }

        // Verify student hasn't actually joined
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s =>
                s.ResourceType == "Booking" &&
                s.ResourceId == booking.Id, cancellationToken);

        var studentJoined = session?.Participants.Any(p => p.UserId == booking.StudentUserId) ?? false;

        if (studentJoined)
            return Result<string>.Failure("Öğrenci seansa katılmış görünüyor. No-show raporu verilemez.");

        // Mark as student no-show
        booking.MarkAsStudentNoShow();
        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "StatusChanged",
            "Confirmed", "StudentNoShow",
            $"Mentor tarafından manuel no-show raporu verildi. Bekleme süresi: {noShowWait}dk.",
            performedByRole: "Mentor");

        // Notify student
        _context.UserNotifications.Add(UserNotification.Create(
            booking.StudentUserId, "StudentNoShow",
            "Randevuya Katılmadınız",
            $"Mentor \"{booking.Mentor.DisplayName}\" sizi randevuya katılmadığınızı bildirdi. Ödeme mentora aktarılacaktır.",
            "Booking", booking.Id));

        // Notify mentor (confirmation)
        _context.UserNotifications.Add(UserNotification.Create(
            booking.MentorUserId, "StudentNoShow",
            "No-Show Raporu Onaylandı",
            $"Öğrenciniz \"{booking.Student.DisplayName}\" için no-show raporu kaydedildi. Ödemeniz işlenecektir.",
            "Booking", booking.Id));

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("📋 Manual StudentNoShow reported by mentor for booking {BookingId}", booking.Id);

        return Result<string>.Success("No-show raporu başarıyla kaydedildi. Ödemeniz işlenecektir.");
    }
}
