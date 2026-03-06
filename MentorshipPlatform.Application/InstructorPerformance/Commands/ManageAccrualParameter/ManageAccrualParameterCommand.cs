using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Commands.ManageAccrualParameter;

// Command
public record ManageAccrualParameterCommand(
    Guid? InstructorId,
    decimal PrivateLessonRate,
    decimal GroupLessonRate,
    decimal VideoContentRate,
    int? BonusThresholdLessons,
    decimal? BonusPercentage
) : IRequest<Result<Guid>>;

// Handler
public class ManageAccrualParameterCommandHandler
    : IRequestHandler<ManageAccrualParameterCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ManageAccrualParameterCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        ManageAccrualParameterCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result<Guid>.Failure("Bu işlem yalnızca admin tarafından yapılabilir.");

        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("Kullanıcı kimliği bulunamadı.");

        // Deactivate existing active parameters for the same InstructorId
        var existingParams = await _context.InstructorAccrualParameters
            .Where(p => p.InstructorId == request.InstructorId && p.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingParams)
        {
            existing.Deactivate();
        }

        // Create new parameter
        var newParam = InstructorAccrualParameter.Create(
            privateLessonRate: request.PrivateLessonRate,
            groupLessonRate: request.GroupLessonRate,
            videoContentRate: request.VideoContentRate,
            updatedBy: _currentUser.UserId.Value,
            instructorId: request.InstructorId,
            bonusThresholdLessons: request.BonusThresholdLessons,
            bonusPercentage: request.BonusPercentage);

        _context.InstructorAccrualParameters.Add(newParam);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(newParam.Id);
    }
}
