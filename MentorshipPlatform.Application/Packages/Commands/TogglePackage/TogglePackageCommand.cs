using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Packages.Commands.TogglePackage;

public record TogglePackageCommand(
    Guid PackageId,
    bool IsActive) : IRequest<Result>;

public class TogglePackageCommandValidator : AbstractValidator<TogglePackageCommand>
{
    public TogglePackageCommandValidator()
    {
        RuleFor(x => x.PackageId).NotEmpty().WithMessage("Paket ID zorunludur.");
    }
}

public class TogglePackageCommandHandler
    : IRequestHandler<TogglePackageCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public TogglePackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        TogglePackageCommand request,
        CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken);

        if (package == null)
            return Result.Failure("Paket bulunamadı.");

        if (request.IsActive)
            package.Activate();
        else
            package.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
