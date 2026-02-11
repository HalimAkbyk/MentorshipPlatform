using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.ApproveVerification;

public record ApproveVerificationCommand(
    Guid VerificationId,
    bool IsApproved,
    string? Notes) : IRequest<Result>;

public class ApproveVerificationCommandHandler : IRequestHandler<ApproveVerificationCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ApproveVerificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        ApproveVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var verification = await _context.MentorVerifications
            .FirstOrDefaultAsync(v => v.Id == request.VerificationId, cancellationToken);

        if (verification == null)
            return Result.Failure("Verification not found");

        if (request.IsApproved)
        {
            verification.Approve(request.Notes);
        }
        else
        {
            if (string.IsNullOrEmpty(request.Notes))
                return Result.Failure("Notes required for rejection");
            
            verification.Reject(request.Notes);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}