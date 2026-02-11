using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetSystemHealth;

public record SystemHealthDto(
    int PendingOrdersCount,
    int StuckBookingsCount,
    int ActiveSessionsCount,
    int NoShowBookingsLast24h,
    int DisputedBookingsCount,
    int FailedPaymentsLast24h,
    int ExpiredBookingsLast24h,
    int CancelledBookingsLast24h,
    int CompletedBookingsLast24h);

public record GetSystemHealthQuery : IRequest<Result<SystemHealthDto>>;

public class GetSystemHealthQueryHandler
    : IRequestHandler<GetSystemHealthQuery, Result<SystemHealthDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSystemHealthQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SystemHealthDto>> Handle(
        GetSystemHealthQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);
        var stuckCutoff = now.AddMinutes(-30);

        var pendingOrders = await _context.Orders
            .CountAsync(o => o.Status == OrderStatus.Pending && o.CreatedAt < now.AddMinutes(-10),
                cancellationToken);

        var stuckBookings = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.PendingPayment && b.CreatedAt < stuckCutoff,
                cancellationToken);

        var activeSessions = await _context.VideoSessions
            .CountAsync(s => s.Status == VideoSessionStatus.Live,
                cancellationToken);

        var noShowLast24h = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.NoShow && b.UpdatedAt >= last24h,
                cancellationToken);

        var disputedCount = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Disputed,
                cancellationToken);

        var failedPayments = await _context.Orders
            .CountAsync(o => o.Status == OrderStatus.Failed && o.UpdatedAt >= last24h,
                cancellationToken);

        var expiredLast24h = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Expired && b.UpdatedAt >= last24h,
                cancellationToken);

        var cancelledLast24h = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Cancelled && b.UpdatedAt >= last24h,
                cancellationToken);

        var completedLast24h = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Completed && b.UpdatedAt >= last24h,
                cancellationToken);

        var dto = new SystemHealthDto(
            pendingOrders,
            stuckBookings,
            activeSessions,
            noShowLast24h,
            disputedCount,
            failedPayments,
            expiredLast24h,
            cancelledLast24h,
            completedLast24h);

        return Result<SystemHealthDto>.Success(dto);
    }
}
