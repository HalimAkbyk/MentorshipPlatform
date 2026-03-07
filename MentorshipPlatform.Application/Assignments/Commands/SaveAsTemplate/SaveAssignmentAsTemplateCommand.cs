using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.SaveAsTemplate;

public record SaveAssignmentAsTemplateCommand(
    Guid AssignmentId,
    string TemplateName) : IRequest<Result<Guid>>;

public class SaveAssignmentAsTemplateCommandValidator : AbstractValidator<SaveAssignmentAsTemplateCommand>
{
    public SaveAssignmentAsTemplateCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty().WithMessage("AssignmentId zorunludur");
        RuleFor(x => x.TemplateName).NotEmpty().WithMessage("Sablon adi zorunludur").MaximumLength(200);
    }
}

public class SaveAssignmentAsTemplateCommandHandler : IRequestHandler<SaveAssignmentAsTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveAssignmentAsTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(SaveAssignmentAsTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var source = await _context.Assignments
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId && x.MentorUserId == _currentUser.UserId.Value, cancellationToken);

        if (source == null)
            return Result<Guid>.Failure("Odev bulunamadi");

        // Create deep copy as template (no booking/class/topic link, no due date)
        var templateAssignment = source.DeepCopyFromTemplate(
            _currentUser.UserId.Value,
            null,
            null,
            null,
            null);

        templateAssignment.SetAsTemplate(request.TemplateName);

        _context.Assignments.Add(templateAssignment);

        // Copy materials
        foreach (var material in source.Materials)
        {
            var newMaterial = AssignmentMaterial.Create(
                templateAssignment.Id,
                material.LibraryItemId,
                material.SortOrder,
                material.IsRequired);
            _context.AssignmentMaterials.Add(newMaterial);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(templateAssignment.Id);
    }
}
