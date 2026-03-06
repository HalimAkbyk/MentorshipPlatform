using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.RejectOfferingPrice;

public record RejectOfferingPriceCommand(
    Guid OfferingId,
    string? Reason) : IRequest<Result>;

public class RejectOfferingPriceCommandValidator : AbstractValidator<RejectOfferingPriceCommand>
{
    public RejectOfferingPriceCommandValidator()
    {
        RuleFor(x => x.OfferingId).NotEmpty().WithMessage("OfferingId zorunludur.");
    }
}

public class RejectOfferingPriceCommandHandler
    : IRequestHandler<RejectOfferingPriceCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RejectOfferingPriceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        RejectOfferingPriceCommand request,
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

        offering.RejectApproval(request.Reason, adminUserId);

        // Notify the mentor
        var message = string.IsNullOrWhiteSpace(request.Reason)
            ? $"\"{offering.Title}\" paketinizin fiyati reddedildi."
            : $"\"{offering.Title}\" paketinizin fiyati reddedildi. Sebep: {request.Reason}";

        var notification = UserNotification.Create(
            offering.MentorUserId,
            "price_rejected",
            "Fiyat Reddedildi",
            message,
            "Offering",
            offering.Id);

        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
