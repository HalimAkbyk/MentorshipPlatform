using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.CreateOffering;

public record CreateOfferingCommand(
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
    string? CoverImageUrl) : IRequest<Result<Guid>>;

public class CreateOfferingCommandValidator : AbstractValidator<CreateOfferingCommand>
{
    public CreateOfferingCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Paket adı zorunludur")
            .MaximumLength(100).WithMessage("Paket adı en fazla 100 karakter olabilir");

        RuleFor(x => x.DurationMin)
            .InclusiveBetween(15, 180).WithMessage("Süre 15 ile 180 dakika arasında olmalıdır");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Ücret 0 veya daha fazla olmalıdır");

        RuleFor(x => x.Subtitle)
            .MaximumLength(150).When(x => x.Subtitle != null);

        RuleFor(x => x.Category)
            .MaximumLength(100).When(x => x.Category != null);

        RuleFor(x => x.MaxBookingDaysAhead)
            .InclusiveBetween(1, 365).WithMessage("Max rezervasyon günü 1-365 arası olmalıdır");

        RuleFor(x => x.MinNoticeHours)
            .InclusiveBetween(0, 72).WithMessage("Minimum bildirim süresi 0-72 saat arası olmalıdır");
    }
}

public class CreateOfferingCommandHandler : IRequestHandler<CreateOfferingCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateOfferingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        CreateOfferingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        // Mentor profili var mı?
        var mentorExists = await _context.MentorProfiles
            .AnyAsync(m => m.UserId == mentorUserId, cancellationToken);
        if (!mentorExists)
            return Result<Guid>.Failure("Mentor profile not found");

        // Mevcut en yüksek SortOrder
        var maxSort = await _context.Offerings
            .Where(o => o.MentorUserId == mentorUserId)
            .MaxAsync(o => (int?)o.SortOrder, cancellationToken) ?? -1;

        var offering = Offering.Create(
            mentorUserId,
            OfferingType.OneToOne,
            request.Title,
            request.DurationMin,
            request.Price,
            request.Description,
            request.Category,
            request.Subtitle,
            request.DetailedDescription,
            request.SessionType);

        offering.SetSortOrder(maxSort + 1);

        if (request.MaxBookingDaysAhead > 0 || request.MinNoticeHours > 0 || request.CoverImageUrl != null)
        {
            offering.Update(
                request.Title,
                request.Description,
                request.DurationMin,
                request.Price,
                request.Category,
                request.Subtitle,
                request.DetailedDescription,
                request.SessionType,
                request.MaxBookingDaysAhead > 0 ? request.MaxBookingDaysAhead : 60,
                request.MinNoticeHours >= 0 ? request.MinNoticeHours : 2,
                request.CoverImageUrl);
        }

        _context.Offerings.Add(offering);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(offering.Id);
    }
}
