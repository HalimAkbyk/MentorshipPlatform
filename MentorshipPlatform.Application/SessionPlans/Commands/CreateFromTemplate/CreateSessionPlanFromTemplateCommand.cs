using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.CreateFromTemplate;

public record CreateSessionPlanFromTemplateCommand(
    Guid TemplateId,
    string? NewTitle,
    Guid? BookingId,
    Guid? GroupClassId) : IRequest<Result<Guid>>;

public class CreateSessionPlanFromTemplateCommandValidator : AbstractValidator<CreateSessionPlanFromTemplateCommand>
{
    public CreateSessionPlanFromTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("TemplateId zorunludur");
        RuleFor(x => x.NewTitle).MaximumLength(200).When(x => x.NewTitle != null);
    }
}

public class CreateSessionPlanFromTemplateCommandHandler : IRequestHandler<CreateSessionPlanFromTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateSessionPlanFromTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateSessionPlanFromTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var template = await _context.SessionPlans
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId
                && x.MentorUserId == _currentUser.UserId.Value
                && x.IsTemplate, cancellationToken);

        if (template == null)
            return Result<Guid>.Failure("Sablon bulunamadi");

        var newPlan = template.DeepCopyFromTemplate(
            _currentUser.UserId.Value,
            request.NewTitle,
            request.BookingId,
            request.GroupClassId);

        _context.SessionPlans.Add(newPlan);

        // Copy materials from template
        foreach (var material in template.Materials)
        {
            var newMaterial = SessionPlanMaterial.Create(
                newPlan.Id,
                material.LibraryItemId,
                material.Phase,
                material.SortOrder,
                material.Note);
            _context.SessionPlanMaterials.Add(newMaterial);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(newPlan.Id);
    }
}
