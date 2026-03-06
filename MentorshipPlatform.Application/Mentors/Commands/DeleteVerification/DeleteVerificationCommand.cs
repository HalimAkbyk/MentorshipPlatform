using MediatR;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Mentors.Commands.DeleteVerification;

[RequiresFeature(FeatureFlags.ExternalMentorRegistration)]
public record DeleteVerificationCommand(Guid VerificationId) : IRequest<Result<bool>>;

public class DeleteVerificationCommandHandler 
    : IRequestHandler<DeleteVerificationCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storageService;
    private readonly ILogger<DeleteVerificationCommandHandler> _logger;

    public DeleteVerificationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IStorageService storageService,
        ILogger<DeleteVerificationCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteVerificationCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // ✅ Verification'ı bul
        var verification = await _context.MentorVerifications
            .FirstOrDefaultAsync(v => v.Id == request.VerificationId, cancellationToken);

        if (verification == null)
        {
            _logger.LogWarning("⚠️ Verification not found - Id: {Id}", request.VerificationId);
            return Result<bool>.Failure("Verification not found");
        }

        // ✅ Sadece kendi verification'ını silebilir
        if (verification.MentorUserId != userId)
        {
            _logger.LogWarning("⚠️ Unauthorized delete attempt - UserId: {UserId}, VerificationId: {VerificationId}", 
                userId, request.VerificationId);
            return Result<bool>.Failure("You can only delete your own verifications");
        }

        // ✅ Sadece onaylanmamış belgeler silinebilir
        if (verification.Status == VerificationStatus.Approved)
        {
            _logger.LogWarning("⚠️ Cannot delete approved verification - Id: {Id}", request.VerificationId);
            return Result<bool>.Failure("Cannot delete approved verifications");
        }

        // ✅ MinIO'dan dosyayı sil
        if (!string.IsNullOrEmpty(verification.DocumentUrl))
        {
            try
            {
                // URL'den fileKey'i çıkar
                var uri = new Uri(verification.DocumentUrl);
                var path = uri.AbsolutePath.TrimStart('/');
                
                // Bucket name'i path'den çıkar
                var segments = path.Split('/', 2);
                if (segments.Length == 2)
                {
                    // Format: "student-documents/guid/filename.pdf" or "transcript-documents/guid/filename.pdf"
                    var fileKey = segments[1];
                    
                    _logger.LogInformation("🗑️ Deleting file and GUID folder from MinIO - FileKey: {FileKey}", fileKey);
                    
                    var deleted = await _storageService.DeleteFileAsync(fileKey, cancellationToken);
                    
                    if (deleted)
                    {
                        _logger.LogInformation("✅ File and GUID folder deleted from MinIO - FileKey: {FileKey}", fileKey);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ File deletion failed (but continuing) - FileKey: {FileKey}", fileKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting file from MinIO - DocumentUrl: {Url}", 
                    verification.DocumentUrl);
                // Dosya silinemese bile verification'ı silmeye devam et
            }
        }

        // ✅ Database'den sil
        _context.MentorVerifications.Remove(verification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("✅ Verification deleted - Id: {Id}, Type: {Type}", 
            verification.Id, 
            verification.Type);

        return Result<bool>.Success(true);
    }
}