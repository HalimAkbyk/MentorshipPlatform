using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IStorageService
{
    Task<UploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string userId,
        string documentType,
        CancellationToken cancellationToken = default);

    Task<string> GetPresignedUrlAsync(
        string fileKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default);
}