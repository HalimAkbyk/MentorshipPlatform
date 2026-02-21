using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Admin.Commands.PublishMentor;

public record PublishMentorCommand(Guid UserId) : IRequest<Result<bool>>;

public class PublishMentorCommandHandler 
    : IRequestHandler<PublishMentorCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PublishMentorCommandHandler> _logger;
    private readonly IEmailService _emailService;

    public PublishMentorCommandHandler(
        IApplicationDbContext context,
        ILogger<PublishMentorCommandHandler> logger,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<Result<bool>> Handle(
        PublishMentorCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ✅ Mentor profile'ı bul
            var mentor = await _context.MentorProfiles
                .Include(m => m.Verifications)
                .FirstOrDefaultAsync(m => m.UserId == request.UserId, cancellationToken);

            if (mentor == null)
            {
                _logger.LogWarning("⚠️ Mentor profile not found - UserId: {UserId}", request.UserId);
                return Result<bool>.Failure("Mentor profile not found");
            }

            // ✅ En az 1 onaylı verification olmalı
            var hasApprovedVerification = mentor.Verifications
                .Any(v => v.Status == VerificationStatus.Approved);

            if (!hasApprovedVerification)
            {
                _logger.LogWarning("⚠️ Cannot publish mentor without approved verifications - UserId: {UserId}", 
                    request.UserId);
                return Result<bool>.Failure("Cannot publish mentor: No approved verifications found");
            }

            // ✅ IsListed = true yap (Publish metodu kullan)
            mentor.Publish();

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Mentor published - UserId: {UserId}, University: {University}",
                request.UserId,
                mentor.University);

            // Send mentor published email
            try
            {
                var mentorUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

                if (mentorUser?.Email != null)
                {
                    await _emailService.SendTemplatedEmailAsync(
                        EmailTemplateKeys.MentorPublished,
                        mentorUser.Email,
                        new Dictionary<string, string>
                        {
                            ["mentorName"] = mentorUser.DisplayName
                        },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send mentor published email for {UserId}", request.UserId);
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error publishing mentor - UserId: {UserId}", request.UserId);
            return Result<bool>.Failure($"Failed to publish mentor: {ex.Message}");
        }
    }
}