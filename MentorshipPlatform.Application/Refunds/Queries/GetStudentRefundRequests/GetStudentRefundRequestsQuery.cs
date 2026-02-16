using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Refunds.Queries.GetStudentRefundRequests;

public record StudentRefundRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderType,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string Status,
    string Reason,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

public record GetStudentRefundRequestsQuery : IRequest<Result<List<StudentRefundRequestDto>>>;

public class GetStudentRefundRequestsQueryHandler
    : IRequestHandler<GetStudentRefundRequestsQuery, Result<List<StudentRefundRequestDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentRefundRequestsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<StudentRefundRequestDto>>> Handle(
        GetStudentRefundRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<StudentRefundRequestDto>>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var refundRequests = await _context.RefundRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new StudentRefundRequestDto(
                r.Id,
                r.OrderId,
                r.Order.Type.ToString(),
                r.RequestedAmount,
                r.ApprovedAmount,
                r.Status.ToString(),
                r.Reason,
                r.CreatedAt,
                r.ProcessedAt))
            .ToListAsync(cancellationToken);

        return Result<List<StudentRefundRequestDto>>.Success(refundRequests);
    }
}
