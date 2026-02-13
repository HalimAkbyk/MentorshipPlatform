using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Domain.Entities;

public class BookingQuestion : BaseEntity
{
    public Guid OfferingId { get; private set; }
    public string QuestionText { get; private set; } = string.Empty;
    public bool IsRequired { get; private set; }
    public int SortOrder { get; private set; }

    private BookingQuestion() { }

    public static BookingQuestion Create(
        Guid offeringId,
        string questionText,
        bool isRequired,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            throw new DomainException("Question text cannot be empty");

        if (questionText.Length > 200)
            throw new DomainException("Question text cannot exceed 200 characters");

        if (sortOrder < 0 || sortOrder > 3)
            throw new DomainException("Sort order must be between 0 and 3 (max 4 questions)");

        return new BookingQuestion
        {
            OfferingId = offeringId,
            QuestionText = questionText,
            IsRequired = isRequired,
            SortOrder = sortOrder
        };
    }

    public void Update(string questionText, bool isRequired, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            throw new DomainException("Question text cannot be empty");

        QuestionText = questionText.Length > 200 ? questionText[..200] : questionText;
        IsRequired = isRequired;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
