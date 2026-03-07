using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Library.Queries.GetLibraryStats;

public record LibraryStatsDto(
    int TotalItems,
    int DocumentCount,
    int VideoCount,
    int LinkCount,
    int TemplateCount,
    long TotalSizeBytes);

public record GetLibraryStatsQuery : IRequest<Result<LibraryStatsDto>>;

public class GetLibraryStatsQueryHandler : IRequestHandler<GetLibraryStatsQuery, Result<LibraryStatsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetLibraryStatsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<LibraryStatsDto>> Handle(GetLibraryStatsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<LibraryStatsDto>.Failure("User not authenticated");

        var items = _context.LibraryItems
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value)
            .Where(x => x.Status == LibraryItemStatus.Active);

        var totalItems = await items.CountAsync(cancellationToken);
        var documentCount = await items.CountAsync(x => x.ItemType == LibraryItemType.Document, cancellationToken);
        var videoCount = await items.CountAsync(x => x.ItemType == LibraryItemType.Video, cancellationToken);
        var linkCount = await items.CountAsync(x => x.ItemType == LibraryItemType.Link, cancellationToken);
        var templateCount = await items.CountAsync(x => x.IsTemplate, cancellationToken);
        var totalSizeBytes = await items
            .Where(x => x.FileSizeBytes.HasValue)
            .SumAsync(x => x.FileSizeBytes!.Value, cancellationToken);

        return Result<LibraryStatsDto>.Success(new LibraryStatsDto(
            totalItems,
            documentCount,
            videoCount,
            linkCount,
            templateCount,
            totalSizeBytes));
    }
}
