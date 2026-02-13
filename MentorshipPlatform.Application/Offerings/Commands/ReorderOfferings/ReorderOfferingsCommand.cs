using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.ReorderOfferings;

public record ReorderOfferingsCommand(List<Guid> OfferingIds) : IRequest<Result<bool>>;

public class ReorderOfferingsCommandValidator : AbstractValidator<ReorderOfferingsCommand>
{
    public ReorderOfferingsCommandValidator()
    {
        RuleFor(x => x.OfferingIds)
            .NotEmpty().WithMessage("En az bir paket ID'si gereklidir");
    }
}

public class ReorderOfferingsCommandHandler : IRequestHandler<ReorderOfferingsCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReorderOfferingsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ReorderOfferingsCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var offerings = await _context.Offerings
            .Where(o => o.MentorUserId == _currentUser.UserId.Value)
            .ToListAsync(ct);

        for (int i = 0; i < request.OfferingIds.Count; i++)
        {
            var offering = offerings.FirstOrDefault(o => o.Id == request.OfferingIds[i]);
            if (offering != null)
                offering.SetSortOrder(i);
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
