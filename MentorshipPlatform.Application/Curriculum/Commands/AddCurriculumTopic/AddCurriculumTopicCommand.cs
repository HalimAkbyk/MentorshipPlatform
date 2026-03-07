using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.AddCurriculumTopic;

public record AddCurriculumTopicCommand(
    Guid WeekId,
    string Title,
    string? Description,
    int? EstimatedMinutes,
    string? ObjectiveText) : IRequest<Result<Guid>>;

public class AddCurriculumTopicCommandValidator : AbstractValidator<AddCurriculumTopicCommand>
{
    public AddCurriculumTopicCommandValidator()
    {
        RuleFor(x => x.WeekId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.ObjectiveText).MaximumLength(1000).When(x => x.ObjectiveText != null);
    }
}

public class AddCurriculumTopicCommandHandler : IRequestHandler<AddCurriculumTopicCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddCurriculumTopicCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddCurriculumTopicCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var week = await _context.CurriculumWeeks
            .Include(x => x.Curriculum)
            .FirstOrDefaultAsync(x => x.Id == request.WeekId, cancellationToken);

        if (week == null)
            return Result<Guid>.Failure("Hafta bulunamadi");

        if (week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("Sadece kendi mufredatiniza konu ekleyebilirsiniz");

        var existingTopicCount = await _context.CurriculumTopics
            .CountAsync(x => x.CurriculumWeekId == request.WeekId, cancellationToken);

        var sortOrder = existingTopicCount + 1;

        var topic = CurriculumTopic.Create(
            request.WeekId,
            request.Title,
            request.Description,
            sortOrder,
            request.EstimatedMinutes,
            request.ObjectiveText);

        _context.CurriculumTopics.Add(topic);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(topic.Id);
    }
}
