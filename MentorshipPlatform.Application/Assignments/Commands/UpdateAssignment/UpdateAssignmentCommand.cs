using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.UpdateAssignment;

public record UpdateAssignmentCommand(
    Guid Id,
    string Title,
    string? Description,
    string? Instructions,
    AssignmentType AssignmentType,
    DifficultyLevel? DifficultyLevel,
    int? EstimatedMinutes,
    DateTime? DueDate,
    int? MaxScore,
    bool AllowLateSubmission,
    int? LatePenaltyPercent,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId) : IRequest<Result<bool>>;

public class UpdateAssignmentCommandValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5000).When(x => x.Description != null);
        RuleFor(x => x.Instructions).MaximumLength(5000).When(x => x.Instructions != null);
        RuleFor(x => x.MaxScore).GreaterThan(0).When(x => x.MaxScore.HasValue);
        RuleFor(x => x.EstimatedMinutes).GreaterThan(0).When(x => x.EstimatedMinutes.HasValue);
        RuleFor(x => x.LatePenaltyPercent).InclusiveBetween(0, 100).When(x => x.LatePenaltyPercent.HasValue);
    }
}

public class UpdateAssignmentCommandHandler : IRequestHandler<UpdateAssignmentCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateAssignmentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (assignment == null)
            return Result<bool>.Failure("Odev bulunamadi");

        if (assignment.MentorUserId != _currentUser.UserId.Value)
            return Result<bool>.Failure("Bu odevi duzenleme yetkiniz yok");

        if (assignment.Status == AssignmentStatus.Closed)
            return Result<bool>.Failure("Kapatilmis odev duzenlenemez");

        assignment.Update(
            request.Title,
            request.Description,
            request.Instructions,
            request.AssignmentType,
            request.DifficultyLevel,
            request.EstimatedMinutes,
            request.DueDate,
            request.MaxScore,
            request.AllowLateSubmission,
            request.LatePenaltyPercent,
            request.BookingId,
            request.GroupClassId,
            request.CurriculumTopicId);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
