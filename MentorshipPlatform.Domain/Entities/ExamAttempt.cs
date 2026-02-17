using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class ExamAttempt : BaseEntity
{
    public Guid ExamId { get; private set; }
    public Exam Exam { get; private set; } = null!;
    public Guid StudentUserId { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string Status { get; private set; } = "InProgress"; // InProgress, Completed, TimedOut

    public int TotalPoints { get; private set; }
    public int EarnedPoints { get; private set; }
    public decimal ScorePercentage { get; private set; }
    public bool Passed { get; private set; }

    private readonly List<ExamAnswer> _answers = new();
    public IReadOnlyCollection<ExamAnswer> Answers => _answers.AsReadOnly();

    private ExamAttempt() { }

    public static ExamAttempt Start(Guid examId, Guid studentUserId)
    {
        return new ExamAttempt
        {
            ExamId = examId,
            StudentUserId = studentUserId,
            StartedAt = DateTime.UtcNow,
            Status = "InProgress"
        };
    }

    public void Complete(int totalPoints, int earnedPoints, int passingScore)
    {
        TotalPoints = totalPoints;
        EarnedPoints = earnedPoints;
        ScorePercentage = totalPoints > 0 ? Math.Round((decimal)earnedPoints / totalPoints * 100, 2) : 0;
        Passed = ScorePercentage >= passingScore;
        CompletedAt = DateTime.UtcNow;
        Status = "Completed";
        UpdatedAt = DateTime.UtcNow;
    }

    public void TimeOut(int totalPoints, int earnedPoints, int passingScore)
    {
        Complete(totalPoints, earnedPoints, passingScore);
        Status = "TimedOut";
    }
}
