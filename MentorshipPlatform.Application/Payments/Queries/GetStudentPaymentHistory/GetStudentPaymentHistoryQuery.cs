using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Payments.Queries.GetStudentPaymentHistory;

public record StudentPaymentDto(
    Guid OrderId,
    string Type,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? ResourceTitle,
    string? MentorName,
    Guid ResourceId,
    decimal? RefundedAmount,
    decimal RefundPercentage,
    string? RefundIneligibleReason,
    string? RefundNote);

public record GetStudentPaymentHistoryQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null
) : IRequest<Result<PaginatedList<StudentPaymentDto>>>;

public class GetStudentPaymentHistoryQueryHandler
    : IRequestHandler<GetStudentPaymentHistoryQuery, Result<PaginatedList<StudentPaymentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentPaymentHistoryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<StudentPaymentDto>>> Handle(
        GetStudentPaymentHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<StudentPaymentDto>>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var query = _context.Orders
            .AsNoTracking()
            .Where(o => o.BuyerUserId == userId);

        // Filter by status
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<OrderStatus>(request.Status, true, out var status))
        {
            query = query.Where(o => o.Status == status);
        }

        // Exclude Pending (incomplete) and Abandoned (user closed popup without paying)
        query = query.Where(o => o.Status != OrderStatus.Pending && o.Status != OrderStatus.Abandoned);

        var orderedQuery = query.OrderByDescending(o => o.CreatedAt);

        var paginatedOrders = await orderedQuery
            .ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Enrich with resource details
        var bookingResourceIds = paginatedOrders.Items
            .Where(o => o.Type == OrderType.Booking)
            .Select(o => o.ResourceId)
            .ToList();

        var courseResourceIds = paginatedOrders.Items
            .Where(o => o.Type == OrderType.Course)
            .Select(o => o.ResourceId)
            .ToList();

        var classResourceIds = paginatedOrders.Items
            .Where(o => o.Type == OrderType.GroupClass)
            .Select(o => o.ResourceId)
            .ToList();

        // Get booking details
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .Where(b => bookingResourceIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

        // Get course enrollment details
        var courseEnrollments = await _context.CourseEnrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => courseResourceIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        // Get group class enrollment details
        var classEnrollments = await _context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => classResourceIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        // Get course mentor names
        var courseMentorIds = courseEnrollments
            .Select(e => e.Course.MentorUserId)
            .Distinct()
            .ToList();
        var courseMentors = await _context.Users
            .AsNoTracking()
            .Where(u => courseMentorIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        // Get pending refund requests for this user's orders
        var orderIds = paginatedOrders.Items.Select(o => o.Id).ToList();
        var pendingRefundOrderIds = await _context.RefundRequests
            .AsNoTracking()
            .Where(r => orderIds.Contains(r.OrderId) && r.Status == RefundRequestStatus.Pending)
            .Select(r => r.OrderId)
            .ToListAsync(cancellationToken);

        var dtos = paginatedOrders.Items.Select(o =>
        {
            string? resourceTitle = null;
            string? mentorName = null;
            decimal refundPercentage = 0m;
            string? refundIneligibleReason = null;
            string? refundNote = null;
            bool isNoShow = false;

            if (o.Type == OrderType.Booking)
            {
                var booking = bookings.FirstOrDefault(b => b.Id == o.ResourceId);
                if (booking != null)
                {
                    resourceTitle = booking.Offering?.Title;
                    mentorName = booking.Mentor?.DisplayName;
                    isNoShow = booking.Status == BookingStatus.NoShow;
                    refundPercentage = booking.CalculateRefundPercentage();
                }
            }
            else if (o.Type == OrderType.Course)
            {
                var enrollment = courseEnrollments.FirstOrDefault(e => e.Id == o.ResourceId);
                if (enrollment != null)
                {
                    resourceTitle = enrollment.Course?.Title;
                    var mentor = courseMentors.FirstOrDefault(u => u.Id == enrollment.Course.MentorUserId);
                    mentorName = mentor?.DisplayName;
                    refundPercentage = enrollment.CalculateCourseRefundPercentage();
                }
            }
            else if (o.Type == OrderType.GroupClass)
            {
                var classEnrollment = classEnrollments.FirstOrDefault(e => e.Id == o.ResourceId);
                if (classEnrollment?.Class != null)
                {
                    resourceTitle = classEnrollment.Class.Title;
                    refundPercentage = classEnrollment.Class.CalculateRefundPercentage();
                }
            }

            // Determine refund ineligibility reason
            if (o.Status != OrderStatus.Paid && o.Status != OrderStatus.PartiallyRefunded)
            {
                refundPercentage = 0m;
            }
            else if (pendingRefundOrderIds.Contains(o.Id))
            {
                refundIneligibleReason = "Bu sipariş için zaten bekleyen bir iade talebi var.";
            }
            else if (refundPercentage <= 0m)
            {
                if (o.Type == OrderType.Booking || o.Type == OrderType.GroupClass)
                    refundIneligibleReason = "Ders saati geçtiği için iade talep edilemez.";
                else if (o.Type == OrderType.Course)
                    refundIneligibleReason = "İade süresi dolmuş veya kurs ilerleme oranı iade limitini aşmış.";
            }
            else if (o.RefundedAmount >= o.AmountTotal)
            {
                refundPercentage = 0m;
                refundIneligibleReason = "Bu siparişin tamamı zaten iade edilmiş.";
            }

            // Add helpful note for NoShow refunds
            if (isNoShow && refundPercentage > 0 && refundIneligibleReason == null)
            {
                refundNote = "Mentor derse katılmadığı için tam iade hakkınız bulunmaktadır.";
            }

            DateTime? paidAt = o.Status == OrderStatus.Paid
                || o.Status == OrderStatus.Refunded
                ? o.UpdatedAt
                : null;

            return new StudentPaymentDto(
                o.Id,
                o.Type.ToString(),
                o.AmountTotal,
                o.Currency,
                o.Status.ToString(),
                o.CreatedAt,
                paidAt,
                resourceTitle,
                mentorName,
                o.ResourceId,
                o.RefundedAmount > 0 ? o.RefundedAmount : null,
                refundPercentage,
                refundIneligibleReason,
                refundNote);
        }).ToList();

        return Result<PaginatedList<StudentPaymentDto>>.Success(
            new PaginatedList<StudentPaymentDto>(
                dtos,
                paginatedOrders.TotalCount,
                paginatedOrders.PageNumber,
                paginatedOrders.PageSize));
    }
}
