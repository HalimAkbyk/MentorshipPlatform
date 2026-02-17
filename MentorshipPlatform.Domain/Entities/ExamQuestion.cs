using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class ExamQuestion : BaseEntity
{
    public Guid ExamId { get; private set; }
    public Exam Exam { get; private set; } = null!;

    public string QuestionText { get; private set; } = string.Empty;
    public string QuestionType { get; private set; } = "SingleChoice"; // SingleChoice, MultipleChoice, TrueFalse, ShortAnswer, Essay
    public string? ImageUrl { get; private set; }
    public int Points { get; private set; } = 10;
    public int SortOrder { get; private set; }

    // JSON serialized options for choice questions: [{"key":"A","text":"Option text","isCorrect":true}, ...]
    public string? OptionsJson { get; private set; }

    // For ShortAnswer: the correct answer text
    public string? CorrectAnswer { get; private set; }

    // Explanation shown after answering
    public string? Explanation { get; private set; }

    private ExamQuestion() { }

    public static ExamQuestion Create(
        Guid examId,
        string questionText,
        string questionType,
        string? imageUrl,
        int points,
        int sortOrder,
        string? optionsJson,
        string? correctAnswer,
        string? explanation)
    {
        return new ExamQuestion
        {
            ExamId = examId,
            QuestionText = questionText,
            QuestionType = questionType,
            ImageUrl = imageUrl,
            Points = points,
            SortOrder = sortOrder,
            OptionsJson = optionsJson,
            CorrectAnswer = correctAnswer,
            Explanation = explanation
        };
    }

    public void Update(string questionText, string questionType, string? imageUrl, int points, int sortOrder, string? optionsJson, string? correctAnswer, string? explanation)
    {
        QuestionText = questionText;
        QuestionType = questionType;
        ImageUrl = imageUrl;
        Points = points;
        SortOrder = sortOrder;
        OptionsJson = optionsJson;
        CorrectAnswer = correctAnswer;
        Explanation = explanation;
        UpdatedAt = DateTime.UtcNow;
    }
}
