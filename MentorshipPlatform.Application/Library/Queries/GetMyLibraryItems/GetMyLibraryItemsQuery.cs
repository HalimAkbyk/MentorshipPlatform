using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Library.Queries.GetMyLibraryItems;

public record LibraryItemDto(
    Guid Id,
    string Title,
    string? Description,
    string ItemType,
    string FileFormat,
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
    bool IsSharedWithStudents,
    int UsageCount,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GetMyLibraryItemsQuery(
    LibraryItemType? ItemType = null,
    FileFormat? FileFormat = null,
    string? Category = null,
    string? Subject = null,
    string? Search = null,
    bool? IsTemplate = null,
    LibraryItemStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<LibraryItemDto>>>;

public class GetMyLibraryItemsQueryHandler : IRequestHandler<GetMyLibraryItemsQuery, Result<PaginatedList<LibraryItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyLibraryItemsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<LibraryItemDto>>> Handle(GetMyLibraryItemsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<LibraryItemDto>>.Failure("User not authenticated");

        var page = PaginatedList<LibraryItemDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<LibraryItemDto>.ClampPageSize(request.PageSize);

        var statusFilter = request.Status ?? LibraryItemStatus.Active;

        var query = _context.LibraryItems
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value)
            .Where(x => x.Status == statusFilter);

        if (request.ItemType.HasValue)
            query = query.Where(x => x.ItemType == request.ItemType.Value);

        if (request.FileFormat.HasValue)
            query = query.Where(x => x.FileFormat == request.FileFormat.Value);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(x => x.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Subject))
            query = query.Where(x => x.Subject == request.Subject);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Title.Contains(request.Search));

        if (request.IsTemplate.HasValue)
            query = query.Where(x => x.IsTemplate == request.IsTemplate.Value);

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LibraryItemDto(
                x.Id,
                x.Title,
                x.Description,
                x.ItemType.ToString(),
                x.FileFormat.ToString(),
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
                x.Status.ToString(),
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<LibraryItemDto>>.Success(
            new PaginatedList<LibraryItemDto>(items, totalCount, page, pageSize));
    }
}
