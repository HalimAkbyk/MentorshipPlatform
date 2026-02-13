using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.DeleteOffering;

public record DeleteOfferingCommand(Guid OfferingId) : IRequest<Result<bool>>;

public class DeleteOfferingCommandHandler : IRequestHandler<DeleteOfferingCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteOfferingCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteOfferingCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.MentorUserId == _currentUser.UserId.Value, ct);

        if (offering == null)
            return Result<bool>.Failure("Offering not found");

        // Aktif booking var mı kontrol et
        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.OfferingId == request.OfferingId &&
                          (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment), ct);

        if (hasActiveBookings)
            return Result<bool>.Failure("Bu pakete ait aktif randevular var. Önce randevuları iptal edin.");

        // Booking sorularını sil (cascade delete yapıyoruz ama güvenlik için)
        var questions = await _context.BookingQuestions
            .Where(q => q.OfferingId == request.OfferingId)
            .ToListAsync(ct);
        _context.BookingQuestions.RemoveRange(questions);

        _context.Offerings.Remove(offering);
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
