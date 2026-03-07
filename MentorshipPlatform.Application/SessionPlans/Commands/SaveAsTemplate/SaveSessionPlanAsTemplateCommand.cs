using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.SaveAsTemplate;

public record SaveSessionPlanAsTemplateCommand(
    Guid SessionPlanId,
    string TemplateName) : IRequest<Result<Guid>>;

public class SaveSessionPlanAsTemplateCommandValidator : AbstractValidator<SaveSessionPlanAsTemplateCommand>
{
    public SaveSessionPlanAsTemplateCommandValidator()
    {
        RuleFor(x => x.SessionPlanId).NotEmpty().WithMessage("SessionPlanId zorunludur");
        RuleFor(x => x.TemplateName).NotEmpty().WithMessage("Sablon adi zorunludur").MaximumLength(200);
    }
}

public class SaveSessionPlanAsTemplateCommandHandler : IRequestHandler<SaveSessionPlanAsTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveSessionPlanAsTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(SaveSessionPlanAsTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var sourcePlan = await _context.SessionPlans
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.SessionPlanId && x.MentorUserId == _currentUser.UserId.Value, cancellationToken);

        if (sourcePlan == null)
            return Result<Guid>.Failure("Oturum plani bulunamadi");

        // Create a deep copy as template
        var templatePlan = SessionPlan.Create(
            _currentUser.UserId.Value,
            sourcePlan.Title,
            preSessionNote: sourcePlan.PreSessionNote,
            sessionObjective: sourcePlan.SessionObjective);

        templatePlan.Update(
            sourcePlan.Title,
            sourcePlan.PreSessionNote,
            sourcePlan.SessionObjective,
            sourcePlan.SessionNotes,
            sourcePlan.AgendaItemsJson,
            sourcePlan.PostSessionSummary,
            null);

        templatePlan.SetAsTemplate(request.TemplateName);

        _context.SessionPlans.Add(templatePlan);

        // Copy materials
        foreach (var material in sourcePlan.Materials)
        {
            var templateMaterial = SessionPlanMaterial.Create(
                templatePlan.Id,
                material.LibraryItemId,
                material.Phase,
                material.SortOrder,
                material.Note);
            _context.SessionPlanMaterials.Add(templateMaterial);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(templatePlan.Id);
    }
}
