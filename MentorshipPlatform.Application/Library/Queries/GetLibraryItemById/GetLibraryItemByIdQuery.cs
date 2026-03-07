using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Library.Queries.GetMyLibraryItems;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Library.Queries.GetLibraryItemById;

public record GetLibraryItemByIdQuery(Guid Id) : IRequest<Result<LibraryItemDto>>;

public class GetLibraryItemByIdQueryHandler : IRequestHandler<GetLibraryItemByIdQuery, Result<LibraryItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetLibraryItemByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<LibraryItemDto>> Handle(GetLibraryItemByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<LibraryItemDto>.Failure("User not authenticated");

        var item = await _context.LibraryItems
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new
            {
                x.Id,
                x.MentorUserId,
                x.Title,
                x.Description,
                ItemType = x.ItemType.ToString(),
                FileFormat = x.FileFormat.ToString(),
                x.FileUrl,
                x.OriginalFileName,
                x.FileSizeBytes,
                x.ExternalUrl,
                x.ThumbnailUrl,
                x.Category,
                x.Subject,
                x.TagsJson,
                x.IsTemplate,
                x.TemplateType,
                x.IsSharedWithStudents,
                x.UsageCount,
                Status = x.Status.ToString(),
                x.CreatedAt,
                x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item == null)
            return Result<LibraryItemDto>.Failure("Library item not found");

        if (item.MentorUserId != _currentUser.UserId.Value)
            return Result<LibraryItemDto>.Failure("You can only view your own library items");

        var dto = new LibraryItemDto(
            item.Id,
            item.Title,
            item.Description,
            item.ItemType,
            item.FileFormat,
            item.FileUrl,
            item.OriginalFileName,
            item.FileSizeBytes,
            item.ExternalUrl,
            item.ThumbnailUrl,
            item.Category,
            item.Subject,
            item.TagsJson,
            item.IsTemplate,
            item.TemplateType,
            item.IsSharedWithStudents,
            item.UsageCount,
            item.Status,
            item.CreatedAt,
            item.UpdatedAt);

        return Result<LibraryItemDto>.Success(dto);
    }
}
