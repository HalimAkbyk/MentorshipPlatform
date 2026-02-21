using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Bookings.Commands.ResolveDispute;

public record ResolveDisputeCommand(
    Guid BookingId,
    string Resolution, // "StudentFavor" or "MentorFavor"
    string? Notes) : IRequest<Result>;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IProcessHistoryService _history;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResolveDisputeCommandHandler> _logger;

    public ResolveDisputeCommandHandler(
        IApplicationDbContext context,
        IProcessHistoryService history,
        IEmailService emailService,
        ILogger<ResolveDisputeCommandHandler> logger)
    {
        _context = context;
        _history = history;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        if (booking.Status != BookingStatus.Disputed)
            return Result.Failure("Bu booking dispute durumunda değil");

        string resolution;
        if (request.Resolution == "StudentFavor")
        {
            // Student wins - booking goes to Cancelled for refund processing
            booking.Cancel($"Dispute çözümü: Öğrenci lehine. {request.Notes}");
            resolution = "Öğrenci lehine çözüldü - iade işlemi başlatılacak";
        }
        else if (request.Resolution == "MentorFavor")
        {
            // Mentor wins - booking goes back to Completed
            booking.Complete();
            resolution = "Mentor lehine çözüldü - ödeme mentor'a aktarılacak";
        }
        else
        {
            return Result.Failure("Geçersiz çözüm tipi. 'StudentFavor' veya 'MentorFavor' olmalı");
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "DisputeResolved",
            "Disputed", booking.Status.ToString(),
            $"{resolution}. Not: {request.Notes ?? "-"}",
            performedByRole: "Admin", ct: cancellationToken);

        // Send dispute resolved emails to both parties
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.DisputeResolved,
                booking.Student.Email!,
                new Dictionary<string, string>
                {
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["resolution"] = resolution
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send dispute resolved email to student for {BookingId}", booking.Id);
        }

        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.DisputeResolved,
                booking.Mentor.Email!,
                new Dictionary<string, string>
                {
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["resolution"] = resolution
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send dispute resolved email to mentor for {BookingId}", booking.Id);
        }

        return Result.Success();
    }
}
