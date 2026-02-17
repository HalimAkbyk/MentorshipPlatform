using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class ExamAnswer : BaseEntity
{
    public Guid AttemptId { get; private set; }
    public ExamAttempt Attempt { get; private set; } = null!;
    public Guid QuestionId { get; private set; }
    public ExamQuestion Question { get; private set; } = null!;

    public string? AnswerText { get; private set; } // For ShortAnswer/Essay
    public string? SelectedOptionsJson { get; private set; } // For choice questions: ["A","C"]

    public bool IsCorrect { get; private set; }
    public int PointsEarned { get; private set; }

    private ExamAnswer() { }

    public static ExamAnswer Create(Guid attemptId, Guid questionId, string? answerText, string? selectedOptionsJson)
    {
        return new ExamAnswer
        {
            AttemptId = attemptId,
            QuestionId = questionId,
            AnswerText = answerText,
            SelectedOptionsJson = selectedOptionsJson
        };
    }

    public void Grade(bool isCorrect, int pointsEarned)
    {
        IsCorrect = isCorrect;
        PointsEarned = pointsEarned;
        UpdatedAt = DateTime.UtcNow;
    }
}
