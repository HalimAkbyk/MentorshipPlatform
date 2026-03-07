using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class AssignmentMaterial : BaseEntity
{
    public Guid AssignmentId { get; private set; }
    public Guid LibraryItemId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsRequired { get; private set; }

    // Navigation
    public Assignment Assignment { get; private set; } = null!;
    public LibraryItem LibraryItem { get; private set; } = null!;

    private AssignmentMaterial() { }

    public static AssignmentMaterial Create(
        Guid assignmentId,
        Guid libraryItemId,
        int sortOrder,
        bool isRequired = true)
    {
        return new AssignmentMaterial
        {
            AssignmentId = assignmentId,
            LibraryItemId = libraryItemId,
            SortOrder = sortOrder,
            IsRequired = isRequired
        };
    }
}
