using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Library.Commands.UpdateLibraryItem;

public record UpdateLibraryItemCommand(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    string? Subject,
    string? TagsJson,
    bool IsTemplate,
    string? TemplateType,
    bool IsSharedWithStudents,
    string? ExternalUrl) : IRequest<Result>;

public class UpdateLibraryItemCommandValidator : AbstractValidator<UpdateLibraryItemCommand>
{
    public UpdateLibraryItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Baslik zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null);
        RuleFor(x => x.Category).MaximumLength(100).When(x => x.Category != null);
        RuleFor(x => x.Subject).MaximumLength(100).When(x => x.Subject != null);
        RuleFor(x => x.TemplateType).MaximumLength(100).When(x => x.TemplateType != null);
    }
}

public class UpdateLibraryItemCommandHandler : IRequestHandler<UpdateLibraryItemCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateLibraryItemCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateLibraryItemCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var item = await _context.LibraryItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (item == null)
            return Result.Failure("Library item not found");

        if (item.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only update your own library items");

        item.Update(
            request.Title,
            request.Description,
            item.ItemType,
            item.FileFormat,
            item.FileUrl,
            item.OriginalFileName,
            item.FileSizeBytes,
            request.ExternalUrl,
            item.ThumbnailUrl,
            request.Category,
            request.Subject,
            request.TagsJson,
            request.IsTemplate,
            request.TemplateType,
            request.IsSharedWithStudents);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
