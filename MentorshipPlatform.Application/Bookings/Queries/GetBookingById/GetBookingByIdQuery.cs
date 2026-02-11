using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Queries.GetBookingById;

public record GetBookingByIdQuery(Guid BookingId) : IRequest<Result<BookingDetailDto>>;

public record BookingDetailDto(
    Guid Id,
    Guid StudentUserId,
    string StudentName,
    Guid MentorUserId,
    string MentorName,
    string? MentorAvatar,
    DateTime StartAt,
    DateTime EndAt,
    int DurationMin,
    BookingStatus Status,
    string OfferingTitle,
    decimal Price,
    string Currency,
    string? CancellationReason,
    DateTime CreatedAt);

public class GetBookingByIdQueryHandler 
    : IRequestHandler<GetBookingByIdQuery, Result<BookingDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetBookingByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<BookingDetailDto>> Handle(
        GetBookingByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<BookingDetailDto>.Failure("User not authenticated");

        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result<BookingDetailDto>.Failure("Booking not found");

        var userId = _currentUser.UserId.Value;
        if (booking.StudentUserId != userId && booking.MentorUserId != userId)
            return Result<BookingDetailDto>.Failure("Unauthorized");

        var dto = new BookingDetailDto(
            booking.Id,
            booking.StudentUserId,
            booking.Student.DisplayName,
            booking.MentorUserId,
            booking.Mentor.DisplayName,
            booking.Mentor.AvatarUrl,
            booking.StartAt,
            booking.EndAt,
            booking.DurationMin,
            booking.Status,
            booking.Offering.Title,
            booking.Offering.PriceAmount,
            booking.Offering.Currency,
            booking.CancellationReason,
            booking.CreatedAt);

        return Result<BookingDetailDto>.Success(dto);
    }
}
