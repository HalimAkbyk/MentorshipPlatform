using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class CurriculumTopicMaterial : BaseEntity
{
    public Guid CurriculumTopicId { get; private set; }
    public Guid LibraryItemId { get; private set; }
    public int SortOrder { get; private set; }
    public string MaterialRole { get; private set; } = "Primary";

    // Navigation
    public CurriculumTopic Topic { get; private set; } = null!;
    public LibraryItem LibraryItem { get; private set; } = null!;

    private CurriculumTopicMaterial() { }

    public static CurriculumTopicMaterial Create(
        Guid curriculumTopicId,
        Guid libraryItemId,
        int sortOrder = 0,
        string materialRole = "Primary")
    {
        return new CurriculumTopicMaterial
        {
            CurriculumTopicId = curriculumTopicId,
            LibraryItemId = libraryItemId,
            SortOrder = sortOrder,
            MaterialRole = materialRole
        };
    }
}
