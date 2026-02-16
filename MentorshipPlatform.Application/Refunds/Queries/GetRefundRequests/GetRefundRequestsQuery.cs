using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Refunds.Queries.GetRefundRequests;

public record AdminRefundRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderType,
    Guid RequestedByUserId,
    string? RequesterName,
    string Reason,
    decimal RequestedAmount,
    decimal OrderTotal,
    decimal AlreadyRefunded,
    string Status,
    string RefundType,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? MentorName,
    string? ResourceTitle,
    string? AdminNotes);

public record GetRefundRequestsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedList<AdminRefundRequestDto>>>;

public class GetRefundRequestsQueryHandler
    : IRequestHandler<GetRefundRequestsQuery, Result<PaginatedList<AdminRefundRequestDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetRefundRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<AdminRefundRequestDto>>> Handle(
        GetRefundRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.RefundRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .AsQueryable();

        // Filter by status
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<RefundRequestStatus>(request.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var orderedQuery = query.OrderByDescending(r => r.CreatedAt);

        var paginated = await orderedQuery
            .ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Enrich with user names and resource details
        var requesterIds = paginated.Items.Select(r => r.RequestedByUserId).Distinct().ToList();
        var requesters = await _context.Users
            .AsNoTracking()
            .Where(u => requesterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        // Get booking/course details for resource titles and mentor names
        var bookingOrderIds = paginated.Items
            .Where(r => r.Order.Type == OrderType.Booking)
            .Select(r => r.Order.ResourceId)
            .Distinct().ToList();

        var courseOrderIds = paginated.Items
            .Where(r => r.Order.Type == OrderType.Course)
            .Select(r => r.Order.ResourceId)
            .Distinct().ToList();

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .Where(b => bookingOrderIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

        var courseEnrollments = await _context.CourseEnrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => courseOrderIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        var courseMentorIds = courseEnrollments
            .Where(e => e.Course != null)
            .Select(e => e.Course.MentorUserId)
            .Distinct().ToList();

        var courseMentors = await _context.Users
            .AsNoTracking()
            .Where(u => courseMentorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var dtos = paginated.Items.Select(r =>
        {
            string? mentorName = null;
            string? resourceTitle = null;

            if (r.Order.Type == OrderType.Booking)
            {
                var booking = bookings.FirstOrDefault(b => b.Id == r.Order.ResourceId);
                mentorName = booking?.Mentor?.DisplayName;
                resourceTitle = booking?.Offering?.Title;
            }
            else if (r.Order.Type == OrderType.Course)
            {
                var enrollment = courseEnrollments.FirstOrDefault(e => e.Id == r.Order.ResourceId);
                resourceTitle = enrollment?.Course?.Title;
                if (enrollment?.Course != null)
                    courseMentors.TryGetValue(enrollment.Course.MentorUserId, out mentorName);
            }

            requesters.TryGetValue(r.RequestedByUserId, out var requesterName);

            return new AdminRefundRequestDto(
                r.Id,
                r.OrderId,
                r.Order.Type.ToString(),
                r.RequestedByUserId,
                requesterName,
                r.Reason,
                r.RequestedAmount,
                r.Order.AmountTotal,
                r.Order.RefundedAmount,
                r.Status.ToString(),
                r.Type.ToString(),
                r.CreatedAt,
                r.ProcessedAt,
                mentorName,
                resourceTitle,
                r.AdminNotes);
        }).ToList();

        return Result<PaginatedList<AdminRefundRequestDto>>.Success(
            new PaginatedList<AdminRefundRequestDto>(
                dtos,
                paginated.TotalCount,
                paginated.PageNumber,
                paginated.PageSize));
    }
}
