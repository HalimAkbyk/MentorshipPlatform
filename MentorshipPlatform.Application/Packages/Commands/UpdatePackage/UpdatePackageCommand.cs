using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Packages.Commands.UpdatePackage;

public record UpdatePackageCommand(
    Guid PackageId,
    string Name,
    string? Description,
    decimal Price,
    int PrivateLessonCredits,
    int GroupLessonCredits,
    int VideoAccessCredits,
    int? ValidityDays,
    int SortOrder = 0) : IRequest<Result>;

public class UpdatePackageCommandValidator : AbstractValidator<UpdatePackageCommand>
{
    public UpdatePackageCommandValidator()
    {
        RuleFor(x => x.PackageId).NotEmpty().WithMessage("Paket ID zorunludur.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Paket adı zorunludur.").MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalıdır.");
        RuleFor(x => x.PrivateLessonCredits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GroupLessonCredits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VideoAccessCredits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidityDays).GreaterThan(0).When(x => x.ValidityDays.HasValue)
            .WithMessage("Geçerlilik süresi 0'dan büyük olmalıdır.");
    }
}

public class UpdatePackageCommandHandler
    : IRequestHandler<UpdatePackageCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdatePackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        UpdatePackageCommand request,
        CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken);

        if (package == null)
            return Result.Failure("Paket bulunamadı.");

        package.Update(
            request.Name,
            request.Description,
            request.Price,
            request.PrivateLessonCredits,
            request.GroupLessonCredits,
            request.VideoAccessCredits,
            request.ValidityDays,
            request.SortOrder);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
