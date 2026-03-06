using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetPendingOfferingApprovals;

public record GetPendingOfferingApprovalsQuery : IRequest<Result<List<PendingOfferingApprovalDto>>>;

public record PendingOfferingApprovalDto(
    Guid Id,
    string Title,
    string MentorName,
    decimal Price,
    string Currency,
    DateTime SubmittedAt);

public class GetPendingOfferingApprovalsQueryHandler
    : IRequestHandler<GetPendingOfferingApprovalsQuery, Result<List<PendingOfferingApprovalDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingOfferingApprovalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PendingOfferingApprovalDto>>> Handle(
        GetPendingOfferingApprovalsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await (
            from o in _context.Offerings.AsNoTracking()
            join u in _context.Users.AsNoTracking() on o.MentorUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            where o.ApprovalStatus == OfferingApprovalStatus.PendingApproval
            orderby o.UpdatedAt descending
            select new PendingOfferingApprovalDto(
                o.Id,
                o.Title,
                u != null ? u.DisplayName : "Bilinmeyen",
                o.PriceAmount,
                o.Currency,
                o.UpdatedAt)
        ).ToListAsync(cancellationToken);

        return Result<List<PendingOfferingApprovalDto>>.Success(items);
    }
}
