using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.AddAssignmentMaterial;

public record AddAssignmentMaterialCommand(
    Guid AssignmentId,
    Guid LibraryItemId,
    bool IsRequired) : IRequest<Result<Guid>>;

public class AddAssignmentMaterialCommandValidator : AbstractValidator<AddAssignmentMaterialCommand>
{
    public AddAssignmentMaterialCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.LibraryItemId).NotEmpty();
    }
}

public class AddAssignmentMaterialCommandHandler : IRequestHandler<AddAssignmentMaterialCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddAssignmentMaterialCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddAssignmentMaterialCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .Include(a => a.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId, cancellationToken);

        if (assignment == null)
            return Result<Guid>.Failure("Odev bulunamadi");

        if (assignment.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("Bu odeve materyal ekleme yetkiniz yok");

        // Check if already linked
        var exists = assignment.Materials.Any(m => m.LibraryItemId == request.LibraryItemId);
        if (exists)
            return Result<Guid>.Failure("Bu materyal zaten ekli");

        var libraryItem = await _context.LibraryItems
            .FirstOrDefaultAsync(x => x.Id == request.LibraryItemId, cancellationToken);

        if (libraryItem == null)
            return Result<Guid>.Failure("Kutuphane materyali bulunamadi");

        var sortOrder = assignment.Materials.Count + 1;

        var material = AssignmentMaterial.Create(
            request.AssignmentId,
            request.LibraryItemId,
            sortOrder,
            request.IsRequired);

        _context.AssignmentMaterials.Add(material);

        // Increment usage count on library item
        libraryItem.IncrementUsageCount();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(material.Id);
    }
}
