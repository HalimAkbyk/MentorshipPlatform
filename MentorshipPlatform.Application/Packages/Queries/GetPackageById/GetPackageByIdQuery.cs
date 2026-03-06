using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Packages.Queries.GetPackages;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Packages.Queries.GetPackageById;

public record GetPackageByIdQuery(Guid PackageId) : IRequest<Result<PackageDto>>;

public class GetPackageByIdQueryHandler
    : IRequestHandler<GetPackageByIdQuery, Result<PackageDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPackageByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PackageDto>> Handle(
        GetPackageByIdQuery request,
        CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .AsNoTracking()
            .Where(p => p.Id == request.PackageId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (package == null)
            return Result<PackageDto>.Failure("Paket bulunamadı.");

        return Result<PackageDto>.Success(package);
    }
}
