using Amazon.S3;
using Amazon.S3.Model;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MentorshipPlatform.Infrastructure.Services;

/// <summary>
/// Cloudflare R2 storage service using AWS S3 SDK (R2 is S3-compatible).
/// </summary>
public class R2StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2Options _options;
    private readonly ILogger<R2StorageService> _logger;

    public R2StorageService(
        IAmazonS3 s3Client,
        IOptions<R2Options> options,
        ILogger<R2StorageService> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string userId,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sanitizedFileName = SanitizeFileName(fileName);

            var folderPrefix = documentType switch
            {
                "University" => "student-documents",
                "course-video" => "courses",
                "avatar" => "avatars",
                _ => "transcript-documents",
            };

            var guid = Guid.NewGuid();
            var fileKey = $"{folderPrefix}/{guid}_{sanitizedFileName}";

            _logger.LogInformation(
                "📤 R2 Upload Starting - Type: {Type}, UserId: {UserId}, Original: {Original}, Sanitized: {Sanitized}, FileKey: {FileKey}",
                documentType, userId, fileName, sanitizedFileName, fileKey);

            // Stream'i byte array'e çevir
            byte[] fileBytes;
            if (fileStream is MemoryStream ms)
            {
                fileBytes = ms.ToArray();
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                fileBytes = memoryStream.ToArray();
            }

            _logger.LogInformation("📦 File size: {Size} bytes", fileBytes.Length);

            if (fileBytes.Length == 0)
            {
                return new UploadResult(false, null, null, "File is empty (0 bytes)");
            }

            // Upload to R2
            using var uploadStream = new MemoryStream(fileBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = fileKey,
                InputStream = uploadStream,
                ContentType = contentType,
                DisablePayloadSigning = true, // Required for R2 compatibility
            };
            putRequest.Headers.ContentLength = fileBytes.Length;

            var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            _logger.LogInformation("✅ R2 PutObject successful - FileKey: {FileKey}, ETag: {ETag}, HttpStatus: {Status}",
                fileKey, response.ETag, response.HttpStatusCode);

            // Verify upload - R2 may not return ContentLength in metadata, so check ETag instead
            try
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = fileKey,
                };
                var metadata = await _s3Client.GetObjectMetadataAsync(metadataRequest, cancellationToken);

                _logger.LogInformation("✅ File verified in R2 - Size: {Size}, ETag: {ETag}, ContentType: {ContentType}",
                    metadata.ContentLength, metadata.ETag, metadata.Headers.ContentType);

                // R2 returns ContentLength correctly for HeadObject
                if (metadata.ContentLength == 0 && metadata.ETag == null)
                {
                    _logger.LogError("❌ File uploaded but verification shows empty file with no ETag");
                    return new UploadResult(false, null, null, "File uploaded but is empty (0 bytes)");
                }
            }
            catch (Exception verifyEx)
            {
                // R2 HeadObject may fail in some edge cases - if upload returned 200, trust it
                _logger.LogWarning(verifyEx, "⚠️ File verification failed but upload succeeded. Continuing...");
            }

            // Generate presigned URL (7 days)
            var presignedUrl = GeneratePresignedUrl(fileKey, TimeSpan.FromDays(7));

            _logger.LogInformation("✅ Presigned URL generated for FileKey: {FileKey}", fileKey);

            return new UploadResult(true, fileKey, presignedUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error uploading file to R2 - FileName: {FileName}", fileName);
            return new UploadResult(false, null, null, ex.Message);
        }
    }

    public Task<string> GetPresignedUrlAsync(
        string fileKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var url = GeneratePresignedUrl(fileKey, expiration);
        return Task.FromResult(url);
    }

    public async Task<bool> DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🗑️ Deleting file from R2 - FileKey: {FileKey}", fileKey);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = fileKey,
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

            _logger.LogInformation("✅ File deleted from R2 - FileKey: {FileKey}", fileKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error deleting file from R2 - FileKey: {FileKey}", fileKey);
            return false;
        }
    }

    public Task<string> GetPresignedUploadUrlAsync(
        string fileKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = fileKey,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Protocol = Protocol.HTTPS,
        };

        var url = _s3Client.GetPreSignedURL(request);
        _logger.LogInformation("📤 Generated presigned UPLOAD URL for FileKey: {FileKey}", fileKey);
        return Task.FromResult(url);
    }

    private string GeneratePresignedUrl(string fileKey, TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = fileKey,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS,
        };

        var url = _s3Client.GetPreSignedURL(request);
        _logger.LogInformation("🔗 Generated presigned URL params: {Params}",
            url.Contains("X-Amz-Algorithm") ? "SigV4 ✅" : "SigV2 ⚠️");
        return url;
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        var extension = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        nameWithoutExt = nameWithoutExt.Normalize(System.Text.NormalizationForm.FormD);

        nameWithoutExt = nameWithoutExt
            .Replace("ğ", "g").Replace("Ğ", "G")
            .Replace("ü", "u").Replace("Ü", "U")
            .Replace("ş", "s").Replace("Ş", "S")
            .Replace("ı", "i").Replace("İ", "I")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ç", "c").Replace("Ç", "C");

        var sanitized = new string(nameWithoutExt
            .Where(c => c < 128)
            .Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '-')
            .ToArray());

        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"-+", "-");
        sanitized = sanitized.Trim('-');

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "file";

        return sanitized + extension.ToLowerInvariant();
    }
}

public class R2Options
{
    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    /// <summary>
    /// Optional custom public URL for the bucket (e.g., via Cloudflare domain).
    /// If not set, presigned S3 URLs will be used.
    /// </summary>
    public string? PublicUrl { get; set; }
}
