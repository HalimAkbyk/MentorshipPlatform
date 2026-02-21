using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Admin.Commands.ApproveVerification;

public record ApproveVerificationCommand(
    Guid VerificationId,
    bool IsApproved,
    string? Notes) : IRequest<Result>;

public class ApproveVerificationCommandHandler : IRequestHandler<ApproveVerificationCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ApproveVerificationCommandHandler> _logger;

    public ApproveVerificationCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<ApproveVerificationCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
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

        // Send verification result email to mentor
        try
        {
            var mentorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == verification.MentorUserId, cancellationToken);

            if (mentorUser?.Email != null)
            {
                var templateKey = request.IsApproved
                    ? EmailTemplateKeys.VerificationApproved
                    : EmailTemplateKeys.VerificationRejected;

                await _emailService.SendTemplatedEmailAsync(
                    templateKey,
                    mentorUser.Email,
                    new Dictionary<string, string>
                    {
                        ["verificationType"] = verification.Type.ToString(),
                        ["reason"] = request.Notes ?? ""
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification result email for {VerificationId}", verification.Id);
        }

        return Result.Success();
    }
}