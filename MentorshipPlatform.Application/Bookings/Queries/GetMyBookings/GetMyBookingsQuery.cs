// File: Application/Bookings/Queries/GetMyBookings/GetMyBookingsQuery.cs
// Bu dosyayı TAMAMEN değiştir

using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Queries.GetMyBookings;

public record GetMyBookingsQuery(BookingStatus? Status) : IRequest<Result<List<BookingDto>>>;

// ✅ UPDATED: BookingDto artık hem student hem mentor bilgisi içeriyor
public record BookingDto(
    Guid Id,
    // Mentor bilgileri (student için)
    Guid MentorUserId,
    string MentorName,
    string? MentorAvatar,
    // Student bilgileri (mentor için) - YENİ EKLENEN
    Guid StudentUserId,
    string StudentName,
    string? StudentAvatar,
    // Seans bilgileri
    DateTime StartAt,
    DateTime EndAt,
    int DurationMin,
    BookingStatus Status,
    // Offering bilgileri
    decimal Price,
    string Currency,
    // Reschedule bilgileri
    bool HasPendingReschedule);

public class GetMyBookingsQueryHandler 
    : IRequestHandler<GetMyBookingsQuery, Result<List<BookingDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyBookingsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<BookingDto>>> Handle(
        GetMyBookingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<BookingDto>>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // ✅ Include hem Student hem Mentor
        var query = _context.Bookings
            .Include(b => b.Mentor)
            .Include(b => b.Student)  // ✅ EKLENEN
            .Include(b => b.Offering)
            .Where(b => b.StudentUserId == userId || b.MentorUserId == userId);

        if (request.Status.HasValue)
        {
            query = query.Where(b => b.Status == request.Status.Value);
        }
        else
        {
            // Default: exclude PendingPayment and Expired bookings (abandoned payment attempts)
            query = query.Where(b => b.Status != BookingStatus.PendingPayment
                                  && b.Status != BookingStatus.Expired);
        }

        var bookings = await query
            .OrderByDescending(b => b.StartAt)
            .Select(b => new BookingDto(
                b.Id,
                // Mentor bilgileri
                b.MentorUserId,
                b.Mentor.DisplayName,
                b.Mentor.AvatarUrl,
                // ✅ Student bilgileri - YENİ EKLENEN
                b.StudentUserId,
                b.Student.DisplayName,
                b.Student.AvatarUrl,
                // Seans bilgileri
                b.StartAt,
                b.EndAt,
                b.DurationMin,
                b.Status,
                // Offering bilgileri
                b.Offering.PriceAmount,
                b.Offering.Currency,
                // Reschedule bilgileri
                b.PendingRescheduleStartAt.HasValue))
            .ToListAsync(cancellationToken);

        return Result<List<BookingDto>>.Success(bookings);
    }
}