using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.ApproveOfferingPrice;

public record ApproveOfferingPriceCommand(
    Guid OfferingId,
    decimal? AdminPrice,
    string? Note) : IRequest<Result>;

public class ApproveOfferingPriceCommandValidator : AbstractValidator<ApproveOfferingPriceCommand>
{
    public ApproveOfferingPriceCommandValidator()
    {
        RuleFor(x => x.OfferingId).NotEmpty().WithMessage("OfferingId zorunludur.");
        RuleFor(x => x.AdminPrice).GreaterThan(0)
            .When(x => x.AdminPrice.HasValue)
            .WithMessage("Admin fiyati 0'dan buyuk olmalidir.");
    }
}

public class ApproveOfferingPriceCommandHandler
    : IRequestHandler<ApproveOfferingPriceCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ApproveOfferingPriceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        ApproveOfferingPriceCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("Kullanici dogrulanamadi.");

        var adminUserId = _currentUser.UserId.Value;

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, cancellationToken);

        if (offering == null)
            return Result.Failure("Paket bulunamadi.");

        if (offering.ApprovalStatus != OfferingApprovalStatus.PendingApproval)
            return Result.Failure("Bu paket onay bekleyen durumda degil.");

        offering.ApprovePrice(request.AdminPrice, request.Note, adminUserId);

        // Notify the mentor
        var notification = UserNotification.Create(
            offering.MentorUserId,
            "price_approved",
            "Fiyat Onaylandi",
            request.AdminPrice.HasValue
                ? $"\"{offering.Title}\" paketinizin fiyati {request.AdminPrice.Value:N2} TRY olarak onaylandi."
                : $"\"{offering.Title}\" paketinizin fiyati onaylandi.",
            "Offering",
            offering.Id);

        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
