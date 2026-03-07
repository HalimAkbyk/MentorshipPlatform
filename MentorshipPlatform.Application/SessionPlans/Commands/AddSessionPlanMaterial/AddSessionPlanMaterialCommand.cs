using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.AddSessionPlanMaterial;

public record AddSessionPlanMaterialCommand(
    Guid SessionPlanId,
    Guid LibraryItemId,
    SessionPhase Phase,
    string? Note) : IRequest<Result<Guid>>;

public class AddSessionPlanMaterialCommandValidator : AbstractValidator<AddSessionPlanMaterialCommand>
{
    public AddSessionPlanMaterialCommandValidator()
    {
        RuleFor(x => x.SessionPlanId).NotEmpty();
        RuleFor(x => x.LibraryItemId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(1000).When(x => x.Note != null);
    }
}

public class AddSessionPlanMaterialCommandHandler : IRequestHandler<AddSessionPlanMaterialCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddSessionPlanMaterialCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddSessionPlanMaterialCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .FirstOrDefaultAsync(x => x.Id == request.SessionPlanId, cancellationToken);

        if (plan == null)
            return Result<Guid>.Failure("Session plan not found");

        if (plan.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("You can only add materials to your own session plans");

        var libraryItem = await _context.LibraryItems
            .FirstOrDefaultAsync(x => x.Id == request.LibraryItemId, cancellationToken);

        if (libraryItem == null)
            return Result<Guid>.Failure("Library item not found");

        // Auto sort order: max existing + 1
        var maxSortOrder = await _context.SessionPlanMaterials
            .Where(x => x.SessionPlanId == request.SessionPlanId && x.Phase == request.Phase)
            .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;

        var material = SessionPlanMaterial.Create(
            request.SessionPlanId,
            request.LibraryItemId,
            request.Phase,
            maxSortOrder + 1,
            request.Note);

        _context.SessionPlanMaterials.Add(material);

        // Increment library item usage count
        libraryItem.IncrementUsageCount();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(material.Id);
    }
}
