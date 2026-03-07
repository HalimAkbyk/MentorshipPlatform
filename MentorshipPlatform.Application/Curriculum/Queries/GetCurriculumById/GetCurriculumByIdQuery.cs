using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Queries.GetCurriculumById;

public record TopicMaterialDto(
    Guid LibraryItemId,
    string Title,
    string ItemType,
    string FileFormat,
    string MaterialRole,
    int SortOrder);

public record CurriculumTopicDto(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    int? EstimatedMinutes,
    string? ObjectiveText,
    Guid? LinkedExamId,
    Guid? LinkedAssignmentId,
    List<TopicMaterialDto> Materials);

public record CurriculumWeekDto(
    Guid Id,
    int WeekNumber,
    string Title,
    string? Description,
    int SortOrder,
    List<CurriculumTopicDto> Topics);

public record CurriculumDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? Subject,
    string? Level,
    int TotalWeeks,
    int? EstimatedHoursPerWeek,
    string? CoverImageUrl,
    string Status,
    bool IsDefault,
    DateTime CreatedAt,
    List<CurriculumWeekDto> Weeks);

public record GetCurriculumByIdQuery(Guid Id) : IRequest<Result<CurriculumDetailDto>>;

public class GetCurriculumByIdQueryHandler : IRequestHandler<GetCurriculumByIdQuery, Result<CurriculumDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCurriculumByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CurriculumDetailDto>> Handle(GetCurriculumByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<CurriculumDetailDto>.Failure("User not authenticated");

        var curriculum = await _context.Curriculums
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new CurriculumDetailDto(
                x.Id,
                x.Title,
                x.Description,
                x.Subject,
                x.Level,
                x.TotalWeeks,
                x.EstimatedHoursPerWeek,
                x.CoverImageUrl,
                x.Status.ToString(),
                x.IsDefault,
                x.CreatedAt,
                x.Weeks.OrderBy(w => w.SortOrder).Select(w => new CurriculumWeekDto(
                    w.Id,
                    w.WeekNumber,
                    w.Title,
                    w.Description,
                    w.SortOrder,
                    w.Topics.OrderBy(t => t.SortOrder).Select(t => new CurriculumTopicDto(
                        t.Id,
                        t.Title,
                        t.Description,
                        t.SortOrder,
                        t.EstimatedMinutes,
                        t.ObjectiveText,
                        t.LinkedExamId,
                        t.LinkedAssignmentId,
                        t.Materials.OrderBy(m => m.SortOrder).Select(m => new TopicMaterialDto(
                            m.LibraryItemId,
                            m.LibraryItem.Title,
                            m.LibraryItem.ItemType.ToString(),
                            m.LibraryItem.FileFormat.ToString(),
                            m.MaterialRole,
                            m.SortOrder
                        )).ToList()
                    )).ToList()
                )).ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (curriculum == null)
            return Result<CurriculumDetailDto>.Failure("Mufredat bulunamadi");

        return Result<CurriculumDetailDto>.Success(curriculum);
    }
}
