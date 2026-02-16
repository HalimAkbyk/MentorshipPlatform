using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.UpdateOffering;

public record UpdateOfferingCommand(
    Guid OfferingId,
    string Title,
    string? Description,
    int DurationMin,
    decimal Price,
    string? Category,
    string? Subtitle,
    string? DetailedDescription,
    string? SessionType,
    int MaxBookingDaysAhead,
    int MinNoticeHours,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform) : IRequest<Result<bool>>;

public class UpdateOfferingCommandValidator : AbstractValidator<UpdateOfferingCommand>
{
    public UpdateOfferingCommandValidator()
    {
        RuleFor(x => x.OfferingId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Paket adı zorunludur")
            .MaximumLength(100);

        RuleFor(x => x.DurationMin)
            .InclusiveBetween(15, 180);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Subtitle)
            .MaximumLength(150).When(x => x.Subtitle != null);

        RuleFor(x => x.MaxBookingDaysAhead)
            .InclusiveBetween(1, 365);

        RuleFor(x => x.MinNoticeHours)
            .InclusiveBetween(0, 72);
    }
}

public class UpdateOfferingCommandHandler : IRequestHandler<UpdateOfferingCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateOfferingCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateOfferingCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.MentorUserId == _currentUser.UserId.Value, ct);

        if (offering == null)
            return Result<bool>.Failure("Offering not found");

        offering.Update(
            request.Title,
            request.Description,
            request.DurationMin,
            request.Price,
            request.Category,
            request.Subtitle,
            request.DetailedDescription,
            request.SessionType,
            request.MaxBookingDaysAhead,
            request.MinNoticeHours,
            request.CoverImageUrl,
            request.CoverImagePosition,
            request.CoverImageTransform);

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
