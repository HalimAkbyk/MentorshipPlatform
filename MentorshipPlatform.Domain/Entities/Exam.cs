using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Exam : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid MentorUserId { get; private set; }

    // Scope: what is this exam tied to?
    public string ScopeType { get; private set; } = "General"; // General, Booking, Course, GroupClass
    public Guid? ScopeId { get; private set; } // BookingId, CourseId, GroupClassId (null for General)

    public int DurationMinutes { get; private set; } = 60;
    public int PassingScore { get; private set; } = 50; // percentage
    public bool IsPublished { get; private set; } = false;
    public bool ShuffleQuestions { get; private set; } = false;
    public bool ShowResults { get; private set; } = true; // show results to student after submission
    public int? MaxAttempts { get; private set; } // null = unlimited

    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }

    private readonly List<ExamQuestion> _questions = new();
    public IReadOnlyCollection<ExamQuestion> Questions => _questions.AsReadOnly();

    private readonly List<ExamAttempt> _attempts = new();
    public IReadOnlyCollection<ExamAttempt> Attempts => _attempts.AsReadOnly();

    private Exam() { }

    public static Exam Create(
        string title,
        string? description,
        Guid mentorUserId,
        string scopeType,
        Guid? scopeId,
        int durationMinutes,
        int passingScore,
        bool shuffleQuestions,
        bool showResults,
        int? maxAttempts)
    {
        return new Exam
        {
            Title = title,
            Description = description,
            MentorUserId = mentorUserId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            DurationMinutes = durationMinutes,
            PassingScore = passingScore,
            ShuffleQuestions = shuffleQuestions,
            ShowResults = showResults,
            MaxAttempts = maxAttempts
        };
    }

    public void Update(string title, string? description, int durationMinutes, int passingScore, bool shuffleQuestions, bool showResults, int? maxAttempts)
    {
        Title = title;
        Description = description;
        DurationMinutes = durationMinutes;
        PassingScore = passingScore;
        ShuffleQuestions = shuffleQuestions;
        ShowResults = showResults;
        MaxAttempts = maxAttempts;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish() { IsPublished = true; UpdatedAt = DateTime.UtcNow; }
    public void Unpublish() { IsPublished = false; UpdatedAt = DateTime.UtcNow; }

    public void SetDates(DateTime? startDate, DateTime? endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }
}
