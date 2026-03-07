using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.UpdateCurriculumTopic;

public record UpdateCurriculumTopicCommand(
    Guid TopicId,
    string Title,
    string? Description,
    int? EstimatedMinutes,
    string? ObjectiveText,
    Guid? LinkedExamId,
    Guid? LinkedAssignmentId) : IRequest<Result>;

public class UpdateCurriculumTopicCommandValidator : AbstractValidator<UpdateCurriculumTopicCommand>
{
    public UpdateCurriculumTopicCommandValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.ObjectiveText).MaximumLength(1000).When(x => x.ObjectiveText != null);
    }
}

public class UpdateCurriculumTopicCommandHandler : IRequestHandler<UpdateCurriculumTopicCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCurriculumTopicCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCurriculumTopicCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var topic = await _context.CurriculumTopics
            .Include(x => x.Week)
                .ThenInclude(x => x.Curriculum)
            .FirstOrDefaultAsync(x => x.Id == request.TopicId, cancellationToken);

        if (topic == null)
            return Result.Failure("Konu bulunamadi");

        if (topic.Week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizdaki konulari guncelleyebilirsiniz");

        topic.Update(
            request.Title,
            request.Description,
            request.EstimatedMinutes,
            request.ObjectiveText,
            request.LinkedExamId,
            request.LinkedAssignmentId);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
