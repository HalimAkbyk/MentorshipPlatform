using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.CreateFromTemplate;

public record CreateAssignmentFromTemplateCommand(
    Guid TemplateId,
    string? NewTitle,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId,
    DateTime? DueDate) : IRequest<Result<Guid>>;

public class CreateAssignmentFromTemplateCommandValidator : AbstractValidator<CreateAssignmentFromTemplateCommand>
{
    public CreateAssignmentFromTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("TemplateId zorunludur");
        RuleFor(x => x.NewTitle).MaximumLength(200).When(x => x.NewTitle != null);
    }
}

public class CreateAssignmentFromTemplateCommandHandler : IRequestHandler<CreateAssignmentFromTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateAssignmentFromTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateAssignmentFromTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var template = await _context.Assignments
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId
                && x.MentorUserId == _currentUser.UserId.Value
                && x.IsTemplate, cancellationToken);

        if (template == null)
            return Result<Guid>.Failure("Sablon bulunamadi");

        var newAssignment = template.DeepCopyFromTemplate(
            _currentUser.UserId.Value,
            request.NewTitle,
            request.BookingId,
            request.GroupClassId,
            request.CurriculumTopicId);

        _context.Assignments.Add(newAssignment);

        // Copy materials from template
        foreach (var material in template.Materials)
        {
            var newMaterial = AssignmentMaterial.Create(
                newAssignment.Id,
                material.LibraryItemId,
                material.SortOrder,
                material.IsRequired);
            _context.AssignmentMaterials.Add(newMaterial);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(newAssignment.Id);
    }
}
