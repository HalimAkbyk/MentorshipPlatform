using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Queries.GetSessionPlanById;

public record SessionPlanMaterialDto(
    Guid Id,
    Guid LibraryItemId,
    string? LibraryItemTitle,
    string ItemType,
    string FileFormat,
    string? FileUrl,
    string Phase,
    int SortOrder,
    string? Note);

public record SessionPlanDetailDto(
    Guid Id,
    Guid MentorUserId,
    string? Title,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId,
    string? PreSessionNote,
    string? SessionObjective,
    string? SessionNotes,
    string? AgendaItemsJson,
    string? PostSessionSummary,
    Guid? LinkedAssignmentId,
    string Status,
    DateTime? SharedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<SessionPlanMaterialDto> PreMaterials,
    List<SessionPlanMaterialDto> DuringMaterials,
    List<SessionPlanMaterialDto> PostMaterials);

public record GetSessionPlanByIdQuery(Guid Id) : IRequest<Result<SessionPlanDetailDto>>;

public class GetSessionPlanByIdQueryHandler : IRequestHandler<GetSessionPlanByIdQuery, Result<SessionPlanDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSessionPlanByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<SessionPlanDetailDto>> Handle(GetSessionPlanByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<SessionPlanDetailDto>.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .AsNoTracking()
            .Include(x => x.Materials)
                .ThenInclude(m => m.LibraryItem)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result<SessionPlanDetailDto>.Failure("Session plan not found");

        // Owner or student of the booking/group class can access (student only if Shared or Completed)
        var isOwner = plan.MentorUserId == _currentUser.UserId.Value;
        if (!isOwner)
        {
            var canAccess = false;

            if (plan.Status >= SessionPlanStatus.Shared)
            {
                if (plan.BookingId.HasValue)
                {
                    canAccess = await _context.Bookings
                        .AnyAsync(b => b.Id == plan.BookingId.Value && b.StudentUserId == _currentUser.UserId.Value, cancellationToken);
                }

                if (!canAccess && plan.GroupClassId.HasValue)
                {
                    canAccess = await _context.ClassEnrollments
                        .AnyAsync(e => e.ClassId == plan.GroupClassId.Value && e.StudentUserId == _currentUser.UserId.Value, cancellationToken);
                }
            }

            if (!canAccess)
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
