using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Application.Library.Commands.CreateLibraryItem;

public record CreateLibraryItemCommand(
    string Title,
    string? Description,
    LibraryItemType ItemType,
    FileFormat FileFormat,
    string? FileUrl,
    string? OriginalFileName,
    long? FileSizeBytes,
    string? ExternalUrl,
    string? ThumbnailUrl,
    string? Category,
    string? Subject,
    string? TagsJson,
    bool IsTemplate,
    string? TemplateType,
    bool IsSharedWithStudents) : IRequest<Result<Guid>>;

public class CreateLibraryItemCommandValidator : AbstractValidator<CreateLibraryItemCommand>
{
    public CreateLibraryItemCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null);
        RuleFor(x => x.Category).MaximumLength(100).When(x => x.Category != null);
        RuleFor(x => x.Subject).MaximumLength(100).When(x => x.Subject != null);
        RuleFor(x => x.TemplateType).MaximumLength(100).When(x => x.TemplateType != null);
    }
}

public class CreateLibraryItemCommandHandler : IRequestHandler<CreateLibraryItemCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateLibraryItemCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateLibraryItemCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var item = LibraryItem.Create(
            _currentUser.UserId.Value,
            request.Title,
            request.ItemType,
            request.FileFormat,
            request.Description,
            request.FileUrl,
            request.OriginalFileName,
            request.FileSizeBytes,
            request.ExternalUrl,
            request.ThumbnailUrl,
            request.Category,
            request.Subject,
            request.TagsJson,
            request.IsTemplate,
            request.TemplateType,
            request.IsSharedWithStudents);

        _context.LibraryItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(item.Id);
    }
}
