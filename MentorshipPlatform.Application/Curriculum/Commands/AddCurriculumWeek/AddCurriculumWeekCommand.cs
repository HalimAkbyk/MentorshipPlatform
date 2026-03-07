using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.AddCurriculumWeek;

public record AddCurriculumWeekCommand(
    Guid CurriculumId,
    string Title,
    string? Description) : IRequest<Result<Guid>>;

public class AddCurriculumWeekCommandValidator : AbstractValidator<AddCurriculumWeekCommand>
{
    public AddCurriculumWeekCommandValidator()
    {
        RuleFor(x => x.CurriculumId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public class AddCurriculumWeekCommandHandler : IRequestHandler<AddCurriculumWeekCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddCurriculumWeekCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddCurriculumWeekCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var curriculum = await _context.Curriculums
            .FirstOrDefaultAsync(x => x.Id == request.CurriculumId, cancellationToken);

        if (curriculum == null)
            return Result<Guid>.Failure("Mufredat bulunamadi");

        if (curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("Sadece kendi mufredatiniza hafta ekleyebilirsiniz");

        var existingWeekCount = await _context.CurriculumWeeks
            .CountAsync(x => x.CurriculumId == request.CurriculumId, cancellationToken);

        var weekNumber = existingWeekCount + 1;

        var week = CurriculumWeek.Create(
            request.CurriculumId,
            weekNumber,
            request.Title,
            request.Description,
            weekNumber);

        _context.CurriculumWeeks.Add(week);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(week.Id);
    }
}
