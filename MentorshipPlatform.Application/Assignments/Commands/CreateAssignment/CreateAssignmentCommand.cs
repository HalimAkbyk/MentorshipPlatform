using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Application.Assignments.Commands.CreateAssignment;

public record CreateAssignmentCommand(
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
    Guid? CurriculumTopicId) : IRequest<Result<Guid>>;

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5000).When(x => x.Description != null);
        RuleFor(x => x.Instructions).MaximumLength(5000).When(x => x.Instructions != null);
        RuleFor(x => x.MaxScore).GreaterThan(0).When(x => x.MaxScore.HasValue);
        RuleFor(x => x.EstimatedMinutes).GreaterThan(0).When(x => x.EstimatedMinutes.HasValue);
        RuleFor(x => x.LatePenaltyPercent).InclusiveBetween(0, 100).When(x => x.LatePenaltyPercent.HasValue);
    }
}

public class CreateAssignmentCommandHandler : IRequestHandler<CreateAssignmentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateAssignmentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var assignment = Assignment.Create(
            _currentUser.UserId.Value,
            request.Title,
            request.AssignmentType,
            request.Description,
            request.Instructions,
            request.DifficultyLevel,
            request.EstimatedMinutes,
            request.DueDate,
            request.MaxScore,
            request.AllowLateSubmission,
            request.LatePenaltyPercent,
            request.BookingId,
            request.GroupClassId,
            request.CurriculumTopicId);

        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(assignment.Id);
    }
}
