using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.SessionPlans.Queries.GetSessionPlanById;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Queries.GetSessionPlanByGroupClass;

public record GetSessionPlanByGroupClassQuery(Guid GroupClassId) : IRequest<Result<SessionPlanDetailDto>>;

public class GetSessionPlanByGroupClassQueryHandler : IRequestHandler<GetSessionPlanByGroupClassQuery, Result<SessionPlanDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSessionPlanByGroupClassQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<SessionPlanDetailDto>> Handle(GetSessionPlanByGroupClassQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<SessionPlanDetailDto>.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .AsNoTracking()
            .Include(x => x.Materials)
                .ThenInclude(m => m.LibraryItem)
            .FirstOrDefaultAsync(x => x.GroupClassId == request.GroupClassId, cancellationToken);

        if (plan == null)
            return Result<SessionPlanDetailDto>.Failure("No session plan found for this group class");

        // Owner or enrolled student can access
        var isOwner = plan.MentorUserId == _currentUser.UserId.Value;
        if (!isOwner)
        {
            if (plan.Status < SessionPlanStatus.Shared)
                return Result<SessionPlanDetailDto>.Failure("This session plan has not been shared yet");

            var isEnrolled = await _context.ClassEnrollments
                .AnyAsync(e => e.ClassId == request.GroupClassId && e.StudentUserId == _currentUser.UserId.Value, cancellationToken);

            if (!isEnrolled)
                return Result<SessionPlanDetailDto>.Failure("You do not have access to this session plan");
        }

        var materials = plan.Materials.Select(m => new SessionPlanMaterialDto(
            m.Id,
            m.LibraryItemId,
            m.LibraryItem?.Title,
            m.LibraryItem?.ItemType.ToString() ?? "",
            m.LibraryItem?.FileFormat.ToString() ?? "",
            m.LibraryItem?.FileUrl,
            m.Phase.ToString(),
            m.SortOrder,
            m.Note)).ToList();

        var dto = new SessionPlanDetailDto(
            plan.Id,
            plan.MentorUserId,
            plan.Title,
            plan.BookingId,
            plan.GroupClassId,
            plan.CurriculumTopicId,
            plan.PreSessionNote,
            plan.SessionObjective,
            plan.SessionNotes,
            plan.AgendaItemsJson,
            plan.PostSessionSummary,
            plan.LinkedAssignmentId,
            plan.Status.ToString(),
            plan.SharedAt,
            plan.CreatedAt,
            plan.UpdatedAt,
            materials.Where(m => m.Phase == SessionPhase.Pre.ToString()).OrderBy(m => m.SortOrder).ToList(),
            materials.Where(m => m.Phase == SessionPhase.During.ToString()).OrderBy(m => m.SortOrder).ToList(),
            materials.Where(m => m.Phase == SessionPhase.Post.ToString()).OrderBy(m => m.SortOrder).ToList());

        return Result<SessionPlanDetailDto>.Success(dto);
    }
}
