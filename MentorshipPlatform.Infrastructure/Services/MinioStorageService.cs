using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace MentorshipPlatform.Infrastructure.Services;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(
        IMinioClient minioClient,
        IOptions<MinioOptions> options,
        ILogger<MinioStorageService> logger)
    {
        _minioClient = minioClient;
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
            // ✅ Dosya adını temizle
            var sanitizedFileName = SanitizeFileName(fileName);

            // ✅ Type'a göre folder structure
            var folderPrefix = documentType switch
            {
                "University" => "student-documents",
                "avatar" => "avatars",
                _ => "transcript-documents"
            };

            // ✅ FileKey: {type-folder}/{guid}/{filename}
            var guid = Guid.NewGuid();
            var fileKey = $"{folderPrefix}/{guid}_{sanitizedFileName}";

            _logger.LogInformation(
                "📤 MinIO Upload Starting - Type: {Type}, UserId: {UserId}, Original: {Original}, Sanitized: {Sanitized}, FileKey: {FileKey}",
                documentType,
                userId,
                fileName,
                sanitizedFileName,
                fileKey);

            // ✅ Stream'i byte array'e çevir (en güvenli yöntem)
            byte[] fileBytes;
            if (fileStream is MemoryStream ms)
            {
                fileBytes = ms.ToArray();
                _logger.LogInformation("📦 Converted MemoryStream to byte array - Size: {Size} bytes",
                    fileBytes.Length);
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                fileBytes = memoryStream.ToArray();
                _logger.LogInformation("📦 Copied stream to byte array - Size: {Size} bytes", fileBytes.Length);
            }

            // ✅ Byte array'den yeni MemoryStream oluştur
            using var uploadStream = new MemoryStream(fileBytes);
            uploadStream.Position = 0;

            _logger.LogInformation(
                "📤 Uploading to MinIO - Stream Length: {Length}, Position: {Position}, CanSeek: {CanSeek}",
                uploadStream.Length,
                uploadStream.Position,
                uploadStream.CanSeek);

            var x = await _minioClient.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(fileKey)
                    .WithStreamData(uploadStream)
                    .WithObjectSize(uploadStream.Length)
                    .WithContentType(contentType),
                cancellationToken);

            _logger.LogInformation("✅ MinIO PutObject successful - FileKey: {FileKey}", fileKey);

            // ✅ DOĞRULAMA: Dosya gerçekten yüklendi mi?
            try
            {
                var statResult = await _minioClient.StatObjectAsync(new StatObjectArgs()
                        .WithBucket(_options.BucketName)
                        .WithObject(fileKey),
                    cancellationToken);

                _logger.LogInformation(
                    "✅ File verified in MinIO - Size: {Size}, ETag: {ETag}, ContentType: {ContentType}",
                    statResult.Size,
                    statResult.ETag,
                    statResult.ContentType);

                if (statResult.Size == 0)
                {
                    _logger.LogError("❌ File uploaded but SIZE IS ZERO!");
                    return new UploadResult(false, null, null, "File uploaded but is empty (0 bytes)");
                }

                if (statResult.Size != fileBytes.Length)
                {
                    _logger.LogWarning("⚠️ Size mismatch - Expected: {Expected}, Got: {Actual}",
                        fileBytes.Length,
                        statResult.Size);
                }
            }
            catch (Exception verifyEx)
            {
                _logger.LogError(verifyEx, "❌ Failed to verify file in MinIO after upload");
                return new UploadResult(false, null, null,
                    $"Upload reported success but verification failed: {verifyEx.Message}");
            }

            // ✅ Presigned URL oluştur (7 gün geçerli)
            var presignedUrl = await _minioClient.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(fileKey)
                    .WithExpiry(60 * 60 * 24 * 7)); // 7 gün

            _logger.LogInformation("✅ Presigned URL generated - Length: {Length}", presignedUrl.Length);

            return new UploadResult(true, fileKey, presignedUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error uploading file to MinIO - FileName: {FileName}", fileName);
            return new UploadResult(false, null, null, ex.Message);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        // ✅ Dosya adı ve extension'ı ayır
        var extension = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // ✅ Unicode normalization (combining characters için)
        nameWithoutExt = nameWithoutExt.Normalize(System.Text.NormalizationForm.FormD);

        // ✅ Türkçe karakterleri ASCII'ye çevir (normalization sonrası)
        nameWithoutExt = nameWithoutExt
            .Replace("ğ", "g").Replace("Ğ", "G")
            .Replace("ü", "u").Replace("Ü", "U")
            .Replace("ş", "s").Replace("Ş", "S")
            .Replace("ı", "i").Replace("İ", "I")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ç", "c").Replace("Ç", "C");

        // ✅ Sadece ASCII karakterleri tut, geri kalanını tire yap
        var sanitized = new string(nameWithoutExt
            .Where(c => c < 128) // Sadece ASCII
            .Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '-')
            .ToArray());

        // ✅ Birden fazla dash'i tek dash'e indir
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"-+", "-");

        // ✅ Başta ve sonda dash varsa kaldır
        sanitized = sanitized.Trim('-');

        // ✅ Boş kaldıysa default isim
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "file";

        // ✅ Extension'ı geri ekle (lowercase)
        return sanitized + extension.ToLowerInvariant();
    }

    public async Task<string> GetPresignedUrlAsync(
        string fileKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var url = await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(fileKey)
            .WithExpiry((int)expiration.TotalSeconds));

        return url;
    }

    public async Task<string> GetPresignedUploadUrlAsync(
        string fileKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var url = await _minioClient.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(fileKey)
            .WithExpiry((int)expiration.TotalSeconds));

        _logger.LogInformation("📤 Generated presigned UPLOAD URL for FileKey: {FileKey}", fileKey);
        return url;
    }

    public async Task<bool> DeleteFileAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🗑️ Deleting file from MinIO - FileKey: {FileKey}", fileKey);


            _logger.LogInformation("🗑️ Deleting file from MinIO - FileKey: {FileKey}", fileKey);
            // Fallback: sadece dosyayı sil
            await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(fileKey),
                cancellationToken);

            _logger.LogInformation("✅ File deleted - FileKey: {FileKey}", fileKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error deleting file from MinIO - FileKey: {FileKey}", fileKey);
            return false;
        }
    }
}

    public class MinioOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public bool UseSSL { get; set; }
    }