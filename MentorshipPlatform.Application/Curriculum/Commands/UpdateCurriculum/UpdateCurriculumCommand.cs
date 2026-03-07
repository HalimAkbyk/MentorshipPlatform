using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.UpdateCurriculum;

public record UpdateCurriculumCommand(
    Guid Id,
    string Title,
    string? Description,
    string? Subject,
    string? Level,
    int TotalWeeks,
    int? EstimatedHoursPerWeek,
    string? CoverImageUrl,
    bool IsDefault) : IRequest<Result>;

public class UpdateCurriculumCommandValidator : AbstractValidator<UpdateCurriculumCommand>
{
    public UpdateCurriculumCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.Subject).MaximumLength(100).When(x => x.Subject != null);
        RuleFor(x => x.Level).MaximumLength(100).When(x => x.Level != null);
        RuleFor(x => x.TotalWeeks).GreaterThan(0);
    }
}

public class UpdateCurriculumCommandHandler : IRequestHandler<UpdateCurriculumCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCurriculumCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCurriculumCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var curriculum = await _context.Curriculums
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (curriculum == null)
            return Result.Failure("Mufredat bulunamadi");

        if (curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizi guncelleyebilirsiniz");

        curriculum.Update(
            request.Title,
            request.Description,
            request.Subject,
            request.Level,
            request.TotalWeeks,
            request.EstimatedHoursPerWeek,
            request.CoverImageUrl,
            request.IsDefault);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
