using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

/// <summary>
/// Fallback storage service when MinIO is not configured (e.g., cloud deployment without object storage).
/// Returns meaningful error messages instead of crashing on DI resolution.
/// </summary>
public class NoOpStorageService : IStorageService
{
    private readonly ILogger<NoOpStorageService> _logger;

    public NoOpStorageService(ILogger<NoOpStorageService> logger)
    {
        _logger = logger;
    }

    public Task<UploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string userId,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "⚠️ File upload attempted but no storage service is configured. " +
            "File: {FileName}, UserId: {UserId}, Type: {DocumentType}. " +
            "Configure MinIO or an S3-compatible storage provider.",
            fileName, userId, documentType);

        return Task.FromResult(new UploadResult(
            false, null, null,
            "Dosya depolama servisi yapılandırılmamış. Lütfen yönetici ile iletişime geçin. " +
            "(Storage service is not configured. Please contact the administrator.)"));
    }

    public Task<string> GetPresignedUrlAsync(
        string fileKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("⚠️ Presigned URL requested but no storage service is configured. FileKey: {FileKey}", fileKey);
        return Task.FromResult(string.Empty);
    }

    public Task<bool> DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("⚠️ File deletion requested but no storage service is configured. FileKey: {FileKey}", fileKey);
        return Task.FromResult(false);
    }
}
