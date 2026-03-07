using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.SaveAsTemplate;

public record SaveCurriculumAsTemplateCommand(
    Guid CurriculumId,
    string TemplateName) : IRequest<Result<Guid>>;

public class SaveCurriculumAsTemplateCommandValidator : AbstractValidator<SaveCurriculumAsTemplateCommand>
{
    public SaveCurriculumAsTemplateCommandValidator()
    {
        RuleFor(x => x.CurriculumId).NotEmpty().WithMessage("CurriculumId zorunludur");
        RuleFor(x => x.TemplateName).NotEmpty().WithMessage("Sablon adi zorunludur").MaximumLength(200);
    }
}

public class SaveCurriculumAsTemplateCommandHandler : IRequestHandler<SaveCurriculumAsTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveCurriculumAsTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(SaveCurriculumAsTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var source = await _context.Curriculums
            .Include(x => x.Weeks)
                .ThenInclude(w => w.Topics)
                    .ThenInclude(t => t.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.CurriculumId && x.MentorUserId == _currentUser.UserId.Value, cancellationToken);

        if (source == null)
            return Result<Guid>.Failure("Mufredat bulunamadi");

        // Create deep copy as template
        var templateCurriculum = source.DeepCopyFromTemplate(_currentUser.UserId.Value, null);
        templateCurriculum.SetAsTemplate(request.TemplateName);

        _context.Curriculums.Add(templateCurriculum);

        // Deep copy weeks, topics, and topic materials
        foreach (var week in source.Weeks.OrderBy(w => w.SortOrder))
        {
            var newWeek = CurriculumWeek.Create(
                templateCurriculum.Id,
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

        return Result<Guid>.Success(templateCurriculum.Id);
    }
}
