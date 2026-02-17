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
    decimal? RefundedAmount);

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

        // Get course mentor names
        var courseMentorIds = courseEnrollments
            .Select(e => e.Course.MentorUserId)
            .Distinct()
            .ToList();
        var courseMentors = await _context.Users
            .AsNoTracking()
            .Where(u => courseMentorIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var dtos = paginatedOrders.Items.Select(o =>
        {
            string? resourceTitle = null;
            string? mentorName = null;

            if (o.Type == OrderType.Booking)
            {
                var booking = bookings.FirstOrDefault(b => b.Id == o.ResourceId);
                if (booking != null)
                {
                    resourceTitle = booking.Offering?.Title;
                    mentorName = booking.Mentor?.DisplayName;
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
                }
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
                null); // RefundedAmount will be added in Faz 3
        }).ToList();

        return Result<PaginatedList<StudentPaymentDto>>.Success(
            new PaginatedList<StudentPaymentDto>(
                dtos,
                paginatedOrders.TotalCount,
                paginatedOrders.PageNumber,
                paginatedOrders.PageSize));
    }
}
