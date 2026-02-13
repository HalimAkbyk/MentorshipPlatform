using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Domain.Entities;

public class BookingQuestionResponse : BaseEntity
{
    public Guid BookingId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string AnswerText { get; private set; } = string.Empty;

    // Navigation
    public BookingQuestion Question { get; private set; } = null!;

    private BookingQuestionResponse() { }

    public static BookingQuestionResponse Create(
        Guid bookingId,
        Guid questionId,
        string answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
            throw new DomainException("Answer text cannot be empty");

        return new BookingQuestionResponse
        {
            BookingId = bookingId,
            QuestionId = questionId,
            AnswerText = answerText.Length > 500 ? answerText[..500] : answerText
        };
    }
}
