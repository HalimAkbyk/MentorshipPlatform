using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Packages.Queries.GetPackages;

public record PackageDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int PrivateLessonCredits,
    int GroupLessonCredits,
    int VideoAccessCredits,
    bool IsActive,
    int? ValidityDays,
    int SortOrder,
    DateTime CreatedAt);

public record GetPackagesQuery(
    bool IncludeInactive = false) : IRequest<Result<List<PackageDto>>>;

public class GetPackagesQueryHandler
    : IRequestHandler<GetPackagesQuery, Result<List<PackageDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPackagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PackageDto>>> Handle(
        GetPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Packages.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(p => p.IsActive);

        var packages = await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .Select(p => new PackageDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.PrivateLessonCredits,
                p.GroupLessonCredits,
                p.VideoAccessCredits,
                p.IsActive,
                p.ValidityDays,
                p.SortOrder,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<PackageDto>>.Success(packages);
    }
}
