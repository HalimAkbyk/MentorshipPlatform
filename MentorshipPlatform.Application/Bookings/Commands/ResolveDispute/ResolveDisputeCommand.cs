using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.ResolveDispute;

public record ResolveDisputeCommand(
    Guid BookingId,
    string Resolution, // "StudentFavor" or "MentorFavor"
    string? Notes) : IRequest<Result>;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IProcessHistoryService _history;

    public ResolveDisputeCommandHandler(
        IApplicationDbContext context,
        IProcessHistoryService history)
    {
        _context = context;
        _history = history;
    }

    public async Task<Result> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
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

        return Result.Success();
    }
}
