using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class LibraryItem : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public LibraryItemType ItemType { get; private set; }
    public FileFormat FileFormat { get; private set; }
    public string? FileUrl { get; private set; }
    public string? OriginalFileName { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string? ExternalUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? Category { get; private set; }
    public string? Subject { get; private set; }
    public string? TagsJson { get; private set; }
    public bool IsTemplate { get; private set; }
    public string? TemplateType { get; private set; }
    public bool IsSharedWithStudents { get; private set; }
    public int UsageCount { get; private set; }
    public LibraryItemStatus Status { get; private set; }

    // Navigation
    public User Mentor { get; private set; } = null!;

    private LibraryItem() { }

    public static LibraryItem Create(
        Guid mentorUserId,
        string title,
        LibraryItemType itemType,
        FileFormat fileFormat,
        string? description = null,
        string? fileUrl = null,
        string? originalFileName = null,
        long? fileSizeBytes = null,
        string? externalUrl = null,
        string? thumbnailUrl = null,
        string? category = null,
        string? subject = null,
        string? tagsJson = null,
        bool isTemplate = false,
        string? templateType = null,
        bool isSharedWithStudents = false)
    {
        return new LibraryItem
        {
            MentorUserId = mentorUserId,
            Title = title,
            ItemType = itemType,
            FileFormat = fileFormat,
            Description = description,
            FileUrl = fileUrl,
            OriginalFileName = originalFileName,
            FileSizeBytes = fileSizeBytes,
            ExternalUrl = externalUrl,
            ThumbnailUrl = thumbnailUrl,
            Category = category,
            Subject = subject,
            TagsJson = tagsJson,
            IsTemplate = isTemplate,
            TemplateType = templateType,
            IsSharedWithStudents = isSharedWithStudents,
            UsageCount = 0,
            Status = LibraryItemStatus.Active
        };
    }

    public void Update(
        string title,
        string? description,
        LibraryItemType itemType,
        FileFormat fileFormat,
        string? fileUrl,
        string? originalFileName,
        long? fileSizeBytes,
        string? externalUrl,
        string? thumbnailUrl,
        string? category,
        string? subject,
        string? tagsJson,
        bool isTemplate,
        string? templateType,
        bool isSharedWithStudents)
    {
        Title = title;
        Description = description;
        ItemType = itemType;
        FileFormat = fileFormat;
        FileUrl = fileUrl;
        OriginalFileName = originalFileName;
        FileSizeBytes = fileSizeBytes;
        ExternalUrl = externalUrl;
        ThumbnailUrl = thumbnailUrl;
        Category = category;
        Subject = subject;
        TagsJson = tagsJson;
        IsTemplate = isTemplate;
        TemplateType = templateType;
        IsSharedWithStudents = isSharedWithStudents;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = LibraryItemStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        Status = LibraryItemStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = LibraryItemStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementUsageCount()
    {
        UsageCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSharedWithStudents(bool shared)
    {
        IsSharedWithStudents = shared;
        UpdatedAt = DateTime.UtcNow;
    }
}
