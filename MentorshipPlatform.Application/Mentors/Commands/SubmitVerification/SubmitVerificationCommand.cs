using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Mentors.Commands.SubmitVerification;

[RequiresFeature(FeatureFlags.ExternalMentorRegistration)]
public record SubmitVerificationCommand(
    string Type, // ✅ String olarak al, handler'da enum'a çevir
    IFormFile? Document) : IRequest<Result<Guid>>;

public class SubmitVerificationCommandValidator : AbstractValidator<SubmitVerificationCommand>
{
    public SubmitVerificationCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("Verification type is required")
            .Must(t => Enum.TryParse<VerificationType>(t, out _))
            .WithMessage("Invalid verification type");
            
        RuleFor(x => x.Document)
            .NotNull()
            .WithMessage("Document is required");
    }
}

public class SubmitVerificationCommandHandler
    : IRequestHandler<SubmitVerificationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storageService;
    private readonly IAdminNotificationService _adminNotification;
    private readonly ILogger<SubmitVerificationCommandHandler> _logger;

    public SubmitVerificationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IStorageService storageService,
        IAdminNotificationService adminNotification,
        ILogger<SubmitVerificationCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _storageService = storageService;
        _adminNotification = adminNotification;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        SubmitVerificationCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        // ✅ String'i enum'a çevir
        if (!Enum.TryParse<VerificationType>(request.Type, out var verificationType))
            return Result<Guid>.Failure("Invalid verification type");

        // Upload document
        string? documentUrl = null;
        if (request.Document != null)
        {
            try
            {
                _logger.LogInformation("📤 Starting file upload - FileName: {FileName}, Size: {Size}, ContentType: {ContentType}", 
                    request.Document.FileName, 
                    request.Document.Length, 
                    request.Document.ContentType);

                // ✅ IFormFile stream'ini MemoryStream'e kopyala
                using var memoryStream = new MemoryStream();
                await request.Document.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0; // Başa sar
                
                _logger.LogInformation("📂 MemoryStream created - Length: {Length}, Position: {Position}, CanRead: {CanRead}", 
                    memoryStream.Length, 
                    memoryStream.Position,
                    memoryStream.CanRead);

                var uploadResult = await _storageService.UploadFileAsync(
                    memoryStream,
                    request.Document.FileName,
                    request.Document.ContentType,
                    mentorUserId.ToString(),
                    verificationType.ToString(),
                    cancellationToken);

                if (!uploadResult.Success)
                {
                    _logger.LogError("❌ File upload failed: {Error}", uploadResult.ErrorMessage);
                    return Result<Guid>.Failure(uploadResult.ErrorMessage ?? "File upload failed");
                }

                documentUrl = uploadResult.PublicUrl;
                _logger.LogInformation("✅ File uploaded successfully - URL: {Url}", documentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception during file upload for {FileName}", request.Document.FileName);
                return Result<Guid>.Failure($"File upload error: {ex.Message}");
            }
        }
        else
        {
            _logger.LogWarning("⚠️ No document provided in request");
        }

        // Create verification
        var verification = MentorVerification.Create(
            mentorUserId,
            verificationType,
            documentUrl);

        _context.MentorVerifications.Add(verification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("✅ Verification created - Id: {Id}, Type: {Type}, MentorUserId: {UserId}",
            verification.Id,
            verificationType,
            mentorUserId);

        // Create grouped admin notification for pending mentor verification
        await _adminNotification.CreateOrUpdateGroupedAsync(
            "MentorVerification",
            "pending-mentor-verifications",
            count => ("Mentor Onayları", $"Bekleyen {count} mentor onayınız var"),
            "MentorVerification", verification.Id,
            cancellationToken);

        return Result<Guid>.Success(verification.Id);
    }
}