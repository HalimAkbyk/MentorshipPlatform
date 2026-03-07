using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.UpdateCurriculumWeek;

public record UpdateCurriculumWeekCommand(
    Guid WeekId,
    string Title,
    string? Description) : IRequest<Result>;

public class UpdateCurriculumWeekCommandValidator : AbstractValidator<UpdateCurriculumWeekCommand>
{
    public UpdateCurriculumWeekCommandValidator()
    {
        RuleFor(x => x.WeekId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public class UpdateCurriculumWeekCommandHandler : IRequestHandler<UpdateCurriculumWeekCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCurriculumWeekCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCurriculumWeekCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var week = await _context.CurriculumWeeks
            .Include(x => x.Curriculum)
            .FirstOrDefaultAsync(x => x.Id == request.WeekId, cancellationToken);

        if (week == null)
            return Result.Failure("Hafta bulunamadi");

        if (week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizdaki haftalari guncelleyebilirsiniz");

        week.Update(request.Title, request.Description);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
