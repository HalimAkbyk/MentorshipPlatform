using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Application.Curriculum.Commands.CreateCurriculum;

public record CreateCurriculumCommand(
    string Title,
    string? Description,
    string? Subject,
    string? Level,
    int TotalWeeks,
    int? EstimatedHoursPerWeek) : IRequest<Result<Guid>>;

public class CreateCurriculumCommandValidator : AbstractValidator<CreateCurriculumCommand>
{
    public CreateCurriculumCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.Subject).MaximumLength(100).When(x => x.Subject != null);
        RuleFor(x => x.Level).MaximumLength(100).When(x => x.Level != null);
        RuleFor(x => x.TotalWeeks).GreaterThan(0).WithMessage("Toplam hafta 0'dan buyuk olmalidir");
    }
}

public class CreateCurriculumCommandHandler : IRequestHandler<CreateCurriculumCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateCurriculumCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateCurriculumCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var curriculum = Domain.Entities.Curriculum.Create(
            _currentUser.UserId.Value,
            request.Title,
            request.Description,
            request.Subject,
            request.Level,
            request.TotalWeeks,
            request.EstimatedHoursPerWeek);

        _context.Curriculums.Add(curriculum);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(curriculum.Id);
    }
}
