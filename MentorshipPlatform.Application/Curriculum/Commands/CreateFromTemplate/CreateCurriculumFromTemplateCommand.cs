using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.CreateFromTemplate;

public record CreateCurriculumFromTemplateCommand(
    Guid TemplateId,
    string? NewTitle) : IRequest<Result<Guid>>;

public class CreateCurriculumFromTemplateCommandValidator : AbstractValidator<CreateCurriculumFromTemplateCommand>
{
    public CreateCurriculumFromTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("TemplateId zorunludur");
        RuleFor(x => x.NewTitle).MaximumLength(200).When(x => x.NewTitle != null);
    }
}

public class CreateCurriculumFromTemplateCommandHandler : IRequestHandler<CreateCurriculumFromTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateCurriculumFromTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateCurriculumFromTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var template = await _context.Curriculums
            .Include(x => x.Weeks)
                .ThenInclude(w => w.Topics)
                    .ThenInclude(t => t.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId
                && x.MentorUserId == _currentUser.UserId.Value
                && x.IsTemplate, cancellationToken);

        if (template == null)
            return Result<Guid>.Failure("Sablon bulunamadi");

        var newCurriculum = template.DeepCopyFromTemplate(_currentUser.UserId.Value, request.NewTitle);

        _context.Curriculums.Add(newCurriculum);

        // Deep copy weeks, topics, and topic materials
        foreach (var week in template.Weeks.OrderBy(w => w.SortOrder))
        {
            var newWeek = CurriculumWeek.Create(
                newCurriculum.Id,
                week.WeekNumber,
                week.Title,
                week.Description,
                week.SortOrder);

            _context.CurriculumWeeks.Add(newWeek);

            foreach (var topic in week.Topics.OrderBy(t => t.SortOrder))
            {
                var newTopic = CurriculumTopic.Create(
                    newWeek.Id,
                    topic.Title,
                    topic.Description,
                    topic.SortOrder,
                    topic.EstimatedMinutes,
                    topic.ObjectiveText);

                _context.CurriculumTopics.Add(newTopic);

                foreach (var material in topic.Materials.OrderBy(m => m.SortOrder))
                {
                    var newMaterial = CurriculumTopicMaterial.Create(
                        newTopic.Id,
                        material.LibraryItemId,
                        material.SortOrder,
                        material.MaterialRole);

                    _context.CurriculumTopicMaterials.Add(newMaterial);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(newCurriculum.Id);
    }
}
