using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Queries.GetAccrualParameters;

// DTO
public class AccrualParameterDto
{
    public Guid Id { get; set; }
    public Guid? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public decimal PrivateLessonRate { get; set; }
    public decimal GroupLessonRate { get; set; }
    public decimal VideoContentRate { get; set; }
    public int? BonusThresholdLessons { get; set; }
    public decimal? BonusPercentage { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Query
public record GetAccrualParametersQuery() : IRequest<Result<List<AccrualParameterDto>>>;

// Handler
public class GetAccrualParametersQueryHandler
    : IRequestHandler<GetAccrualParametersQuery, Result<List<AccrualParameterDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAccrualParametersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AccrualParameterDto>>> Handle(
        GetAccrualParametersQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result<List<AccrualParameterDto>>.Failure("Bu işlem yalnızca admin tarafından yapılabilir.");

        var parameters = await _context.InstructorAccrualParameters
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.InstructorId.HasValue ? 1 : 0) // Global first
            .ThenBy(p => p.ValidFrom)
            .ToListAsync(cancellationToken);

        // Resolve instructor names for per-instructor parameters
        var instructorIds = parameters
            .Where(p => p.InstructorId.HasValue)
            .Select(p => p.InstructorId!.Value)
            .Distinct()
            .ToList();

        var instructorNames = instructorIds.Count > 0
            ? await _context.Users
                .AsNoTracking()
                .Where(u => instructorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken)
            : new Dictionary<Guid, string>();

        var items = parameters.Select(p => new AccrualParameterDto
        {
            Id = p.Id,
            InstructorId = p.InstructorId,
            InstructorName = p.InstructorId.HasValue
                ? instructorNames.GetValueOrDefault(p.InstructorId.Value, "Unknown")
                : null,
            PrivateLessonRate = p.PrivateLessonRate,
            GroupLessonRate = p.GroupLessonRate,
            VideoContentRate = p.VideoContentRate,
            BonusThresholdLessons = p.BonusThresholdLessons,
            BonusPercentage = p.BonusPercentage,
            IsActive = p.IsActive,
            ValidFrom = p.ValidFrom,
            ValidTo = p.ValidTo,
            UpdatedBy = p.UpdatedBy,
            CreatedAt = p.CreatedAt
        }).ToList();

        return Result<List<AccrualParameterDto>>.Success(items);
    }
}
