using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.SubmitOfferingForApproval;

[RequiresFeature(FeatureFlags.PriceApprovalRequired)]
public record SubmitOfferingForApprovalCommand(Guid OfferingId) : IRequest<Result>;

public class SubmitOfferingForApprovalCommandValidator : AbstractValidator<SubmitOfferingForApprovalCommand>
{
    public SubmitOfferingForApprovalCommandValidator()
    {
        RuleFor(x => x.OfferingId).NotEmpty().WithMessage("OfferingId zorunludur.");
    }
}

public class SubmitOfferingForApprovalCommandHandler
    : IRequestHandler<SubmitOfferingForApprovalCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubmitOfferingForApprovalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        SubmitOfferingForApprovalCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("Kullanici dogrulanamadi.");

        var userId = _currentUser.UserId.Value;

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, cancellationToken);

        if (offering == null)
            return Result.Failure("Paket bulunamadi.");

        if (offering.MentorUserId != userId)
            return Result.Failure("Bu paket size ait degil.");

        if (offering.ApprovalStatus == OfferingApprovalStatus.PendingApproval)
            return Result.Failure("Bu paket zaten onay bekliyor.");

        offering.SubmitForApproval();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
