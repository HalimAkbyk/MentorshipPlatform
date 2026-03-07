using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Application.SessionPlans.Commands.CreateSessionPlan;

public record CreateSessionPlanCommand(
    string? Title,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId,
    string? PreSessionNote,
    string? SessionObjective) : IRequest<Result<Guid>>;

public class CreateSessionPlanCommandValidator : AbstractValidator<CreateSessionPlanCommand>
{
    public CreateSessionPlanCommandValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);
        RuleFor(x => x.PreSessionNote).MaximumLength(5000).When(x => x.PreSessionNote != null);
        RuleFor(x => x.SessionObjective).MaximumLength(5000).When(x => x.SessionObjective != null);
    }
}

public class CreateSessionPlanCommandHandler : IRequestHandler<CreateSessionPlanCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateSessionPlanCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateSessionPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var plan = SessionPlan.Create(
            _currentUser.UserId.Value,
            request.Title,
            request.BookingId,
            request.GroupClassId,
            request.CurriculumTopicId,
            request.PreSessionNote,
            request.SessionObjective);

        _context.SessionPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(plan.Id);
    }
}
