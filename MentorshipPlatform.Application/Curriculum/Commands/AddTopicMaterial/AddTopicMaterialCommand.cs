using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.AddTopicMaterial;

public record AddTopicMaterialCommand(
    Guid TopicId,
    Guid LibraryItemId,
    string MaterialRole = "Primary") : IRequest<Result<Guid>>;

public class AddTopicMaterialCommandValidator : AbstractValidator<AddTopicMaterialCommand>
{
    public AddTopicMaterialCommandValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.LibraryItemId).NotEmpty();
        RuleFor(x => x.MaterialRole).NotEmpty().MaximumLength(50);
    }
}

public class AddTopicMaterialCommandHandler : IRequestHandler<AddTopicMaterialCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddTopicMaterialCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddTopicMaterialCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var topic = await _context.CurriculumTopics
            .Include(x => x.Week)
                .ThenInclude(x => x.Curriculum)
            .FirstOrDefaultAsync(x => x.Id == request.TopicId, cancellationToken);

        if (topic == null)
            return Result<Guid>.Failure("Konu bulunamadi");

        if (topic.Week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("Sadece kendi mufredatiniza materyal ekleyebilirsiniz");

        var libraryItem = await _context.LibraryItems
            .FirstOrDefaultAsync(x => x.Id == request.LibraryItemId, cancellationToken);

        if (libraryItem == null)
            return Result<Guid>.Failure("Kutuphane ogesi bulunamadi");

        // Check if already linked
        var alreadyLinked = await _context.CurriculumTopicMaterials
            .AnyAsync(x => x.CurriculumTopicId == request.TopicId && x.LibraryItemId == request.LibraryItemId, cancellationToken);

        if (alreadyLinked)
            return Result<Guid>.Failure("Bu materyal zaten bu konuya eklenmis");

        var existingCount = await _context.CurriculumTopicMaterials
            .CountAsync(x => x.CurriculumTopicId == request.TopicId, cancellationToken);

        var material = CurriculumTopicMaterial.Create(
            request.TopicId,
            request.LibraryItemId,
            existingCount + 1,
            request.MaterialRole);

        _context.CurriculumTopicMaterials.Add(material);

        // Increment library item usage count
        libraryItem.IncrementUsageCount();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(material.Id);
    }
}
