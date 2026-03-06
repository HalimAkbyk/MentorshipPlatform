using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Application.Packages.Commands.CreatePackage;

public record CreatePackageCommand(
    string Name,
    string? Description,
    decimal Price,
    int PrivateLessonCredits,
    int GroupLessonCredits,
    int VideoAccessCredits,
    int? ValidityDays,
    int SortOrder = 0) : IRequest<Result<Guid>>;

public class CreatePackageCommandValidator : AbstractValidator<CreatePackageCommand>
{
    public CreatePackageCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Paket adı zorunludur.").MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalıdır.");
        RuleFor(x => x.PrivateLessonCredits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GroupLessonCredits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VideoAccessCredits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidityDays).GreaterThan(0).When(x => x.ValidityDays.HasValue)
            .WithMessage("Geçerlilik süresi 0'dan büyük olmalıdır.");
    }
}

public class CreatePackageCommandHandler
    : IRequestHandler<CreatePackageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreatePackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        CreatePackageCommand request,
        CancellationToken cancellationToken)
    {
        var package = Package.Create(
            request.Name,
            request.Description,
            request.Price,
            request.PrivateLessonCredits,
            request.GroupLessonCredits,
            request.VideoAccessCredits,
            request.ValidityDays,
            request.SortOrder);

        _context.Packages.Add(package);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(package.Id);
    }
}
