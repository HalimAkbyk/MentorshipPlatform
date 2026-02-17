using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Persistence;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExamsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ─── Mentor Endpoints ────────────────────────────────────────────────

    /// <summary>
    /// Create a new exam (Mentor only)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
    {
        var mentorId = _currentUser.UserId!.Value;

        var validScopeTypes = new[] { "General", "Booking", "Course", "GroupClass" };
        if (!validScopeTypes.Contains(request.ScopeType))
            return BadRequest(new { errors = new[] { "Invalid ScopeType. Must be General, Booking, Course, or GroupClass." } });

        if (request.ScopeType != "General" && request.ScopeId == null)
            return BadRequest(new { errors = new[] { "ScopeId is required when ScopeType is not General." } });

        var exam = Exam.Create(
            request.Title,
            request.Description,
            mentorId,
            request.ScopeType,
            request.ScopeId,
            request.DurationMinutes,
            request.PassingScore,
            request.ShuffleQuestions,
            request.ShowResults,
            request.MaxAttempts);

        if (request.StartDate.HasValue || request.EndDate.HasValue)
            exam.SetDates(request.StartDate, request.EndDate);

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        return Ok(new { id = exam.Id });
    }

    /// <summary>
    /// Update an exam (Mentor only, must own)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> UpdateExam(Guid id, [FromBody] UpdateExamRequest request)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        exam.Update(
            request.Title,
            request.Description,
            request.DurationMinutes,
            request.PassingScore,
            request.ShuffleQuestions,
            request.ShowResults,
            request.MaxAttempts);

        if (request.StartDate.HasValue || request.EndDate.HasValue)
            exam.SetDates(request.StartDate, request.EndDate);

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete an exam (Mentor only, no attempts allowed)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> DeleteExam(Guid id)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams
            .Include(e => e.Attempts)
            .FirstOrDefaultAsync(e => e.Id == id && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        if (exam.Attempts.Any())
            return BadRequest(new { errors = new[] { "Cannot delete exam that has attempts. Unpublish it instead." } });

        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Publish an exam (Mentor only)
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> PublishExam(Guid id)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams
            .Include(e => e.Questions)
            .FirstOrDefaultAsync(e => e.Id == id && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        if (!exam.Questions.Any())
            return BadRequest(new { errors = new[] { "Cannot publish an exam with no questions." } });

        exam.Publish();
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Unpublish an exam (Mentor only)
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> UnpublishExam(Guid id)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        exam.Unpublish();
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// List mentor's own exams (paginated, filtered by scopeType)
    /// </summary>
    [HttpGet("my-exams")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetMyExams(
        [FromQuery] string? scopeType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var mentorId = _currentUser.UserId!.Value;
        var query = _db.Exams
            .Where(e => e.MentorUserId == mentorId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(scopeType))
            query = query.Where(e => e.ScopeType == scopeType);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.ScopeType,
                e.ScopeId,
                e.DurationMinutes,
                e.PassingScore,
                e.IsPublished,
                e.ShuffleQuestions,
                e.ShowResults,
                e.MaxAttempts,
                e.StartDate,
                e.EndDate,
                QuestionCount = e.Questions.Count(),
                AttemptCount = e.Attempts.Count(),
                e.CreatedAt,
                e.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    /// <summary>
    /// Get exam with all questions (Mentor only — includes correct answers)
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetExamDetail(Guid id)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams
            .Where(e => e.Id == id && e.MentorUserId == mentorId)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.ScopeType,
                e.ScopeId,
                e.DurationMinutes,
                e.PassingScore,
                e.IsPublished,
                e.ShuffleQuestions,
                e.ShowResults,
                e.MaxAttempts,
                e.StartDate,
                e.EndDate,
                Questions = e.Questions.OrderBy(q => q.SortOrder).Select(q => new
                {
                    q.Id,
                    q.QuestionText,
                    q.QuestionType,
                    q.ImageUrl,
                    q.Points,
                    q.SortOrder,
                    q.OptionsJson,
                    q.CorrectAnswer,
                    q.Explanation
                }).ToList(),
                AttemptCount = e.Attempts.Count(),
                e.CreatedAt,
                e.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });
        return Ok(exam);
    }

    // ─── Question Management ─────────────────────────────────────────────

    /// <summary>
    /// Add a question to an exam (Mentor only)
    /// </summary>
    [HttpPost("{examId:guid}/questions")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> AddQuestion(Guid examId, [FromBody] AddQuestionRequest request)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        var validTypes = new[] { "SingleChoice", "MultipleChoice", "TrueFalse", "ShortAnswer", "Essay" };
        if (!validTypes.Contains(request.QuestionType))
            return BadRequest(new { errors = new[] { "Invalid QuestionType." } });

        string? optionsJson = null;
        if (request.Options != null && request.Options.Any())
            optionsJson = JsonSerializer.Serialize(request.Options, JsonOptions);

        var question = ExamQuestion.Create(
            examId,
            request.QuestionText,
            request.QuestionType,
            request.ImageUrl,
            request.Points,
            request.SortOrder,
            optionsJson,
            request.CorrectAnswer,
            request.Explanation);

        _db.ExamQuestions.Add(question);
        await _db.SaveChangesAsync();

        return Ok(new { id = question.Id });
    }

    /// <summary>
    /// Update a question (Mentor only)
    /// </summary>
    [HttpPut("{examId:guid}/questions/{questionId:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> UpdateQuestion(Guid examId, Guid questionId, [FromBody] AddQuestionRequest request)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        var question = await _db.ExamQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.ExamId == examId);
        if (question == null) return NotFound(new { errors = new[] { "Question not found." } });

        string? optionsJson = null;
        if (request.Options != null && request.Options.Any())
            optionsJson = JsonSerializer.Serialize(request.Options, JsonOptions);

        question.Update(
            request.QuestionText,
            request.QuestionType,
            request.ImageUrl,
            request.Points,
            request.SortOrder,
            optionsJson,
            request.CorrectAnswer,
            request.Explanation);

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete a question (Mentor only)
    /// </summary>
    [HttpDelete("{examId:guid}/questions/{questionId:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> DeleteQuestion(Guid examId, Guid questionId)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        var question = await _db.ExamQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.ExamId == examId);
        if (question == null) return NotFound(new { errors = new[] { "Question not found." } });

        _db.ExamQuestions.Remove(question);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Get all attempts/results for an exam (Mentor only)
    /// </summary>
    [HttpGet("{examId:guid}/results")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetExamResults(Guid examId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var mentorId = _currentUser.UserId!.Value;
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.MentorUserId == mentorId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        var query = _db.ExamAttempts
            .Where(a => a.ExamId == examId)
            .AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.StudentUserId,
                a.StartedAt,
                a.CompletedAt,
                a.Status,
                a.TotalPoints,
                a.EarnedPoints,
                a.ScorePercentage,
                a.Passed
            })
            .ToListAsync();

        // Resolve student names
        var studentIds = items.Select(i => i.StudentUserId).Distinct().ToList();
        var studentNames = await _db.Users
            .Where(u => studentIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var result = items.Select(a => new
        {
            a.Id,
            a.StudentUserId,
            StudentName = studentNames.GetValueOrDefault(a.StudentUserId, "?"),
            a.StartedAt,
            a.CompletedAt,
            a.Status,
            a.TotalPoints,
            a.EarnedPoints,
            a.ScorePercentage,
            a.Passed
        });

        return Ok(new { items = result, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    // ─── Student Endpoints ───────────────────────────────────────────────

    /// <summary>
    /// List available exams for student (filtered by scope)
    /// </summary>
    [HttpGet("available")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> GetAvailableExams(
        [FromQuery] string? scopeType = null,
        [FromQuery] Guid? scopeId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var studentId = _currentUser.UserId!.Value;
        var now = DateTime.UtcNow;

        var query = _db.Exams
            .Where(e => e.IsPublished)
            .Where(e => e.StartDate == null || e.StartDate <= now)
            .Where(e => e.EndDate == null || e.EndDate >= now)
            .AsQueryable();

        if (!string.IsNullOrEmpty(scopeType))
            query = query.Where(e => e.ScopeType == scopeType);

        if (scopeId.HasValue)
            query = query.Where(e => e.ScopeId == scopeId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.ScopeType,
                e.ScopeId,
                e.DurationMinutes,
                e.PassingScore,
                e.MaxAttempts,
                e.StartDate,
                e.EndDate,
                QuestionCount = e.Questions.Count(),
                e.MentorUserId,
                e.CreatedAt
            })
            .ToListAsync();

        // Resolve mentor names
        var mentorIds = items.Select(i => i.MentorUserId).Distinct().ToList();
        var mentorNames = await _db.Users
            .Where(u => mentorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        // Get attempt counts per exam for this student
        var examIds = items.Select(i => i.Id).ToList();
        var attemptCounts = await _db.ExamAttempts
            .Where(a => a.StudentUserId == studentId && examIds.Contains(a.ExamId))
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ExamId, g => g.Count);

        var result = items.Select(e => new
        {
            e.Id,
            e.Title,
            e.Description,
            e.ScopeType,
            e.ScopeId,
            e.DurationMinutes,
            e.PassingScore,
            e.MaxAttempts,
            e.StartDate,
            e.EndDate,
            e.QuestionCount,
            e.MentorUserId,
            MentorName = mentorNames.GetValueOrDefault(e.MentorUserId, "?"),
            MyAttemptCount = attemptCounts.GetValueOrDefault(e.Id, 0),
            e.CreatedAt
        });

        return Ok(new { items = result, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    /// <summary>
    /// Get exam info for student (without correct answers)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExam(Guid id)
    {
        var exam = await _db.Exams
            .Where(e => e.Id == id && e.IsPublished)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.ScopeType,
                e.ScopeId,
                e.DurationMinutes,
                e.PassingScore,
                e.ShuffleQuestions,
                e.ShowResults,
                e.MaxAttempts,
                e.StartDate,
                e.EndDate,
                e.MentorUserId,
                QuestionCount = e.Questions.Count(),
                TotalPoints = e.Questions.Sum(q => q.Points),
                Questions = e.Questions.OrderBy(q => q.SortOrder).Select(q => new
                {
                    q.Id,
                    q.QuestionText,
                    q.QuestionType,
                    q.ImageUrl,
                    q.Points,
                    q.SortOrder,
                    // Strip isCorrect from options for students
                    q.OptionsJson,
                    q.Explanation
                    // NOTE: CorrectAnswer is NOT included
                }).ToList(),
                e.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (exam == null) return NotFound(new { errors = new[] { "Exam not found or not published." } });

        // Strip isCorrect from OptionsJson for student view
        var sanitizedQuestions = exam.Questions.Select(q =>
        {
            List<object>? sanitizedOptions = null;
            if (!string.IsNullOrEmpty(q.OptionsJson))
            {
                try
                {
                    var options = JsonSerializer.Deserialize<List<QuestionOptionDto>>(q.OptionsJson, JsonOptions);
                    if (options != null)
                    {
                        sanitizedOptions = options.Select(o => (object)new { o.Key, o.Text }).ToList();
                    }
                }
                catch { /* keep raw if deserialization fails */ }
            }

            return new
            {
                q.Id,
                q.QuestionText,
                q.QuestionType,
                q.ImageUrl,
                q.Points,
                q.SortOrder,
                Options = sanitizedOptions
            };
        }).ToList();

        // Resolve mentor name
        var mentorName = await _db.Users
            .Where(u => u.Id == exam.MentorUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            exam.Id,
            exam.Title,
            exam.Description,
            exam.ScopeType,
            exam.ScopeId,
            exam.DurationMinutes,
            exam.PassingScore,
            exam.ShuffleQuestions,
            exam.ShowResults,
            exam.MaxAttempts,
            exam.StartDate,
            exam.EndDate,
            exam.MentorUserId,
            MentorName = mentorName ?? "?",
            exam.QuestionCount,
            exam.TotalPoints,
            Questions = sanitizedQuestions,
            exam.CreatedAt
        });
    }

    /// <summary>
    /// Start an exam attempt (Student only)
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> StartAttempt(Guid id)
    {
        var studentId = _currentUser.UserId!.Value;
        var now = DateTime.UtcNow;

        var exam = await _db.Exams
            .Include(e => e.Questions)
            .FirstOrDefaultAsync(e => e.Id == id && e.IsPublished);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found or not published." } });

        // Check date window
        if (exam.StartDate.HasValue && now < exam.StartDate)
            return BadRequest(new { errors = new[] { "This exam has not started yet." } });
        if (exam.EndDate.HasValue && now > exam.EndDate)
            return BadRequest(new { errors = new[] { "This exam has ended." } });

        // Check if student has an in-progress attempt
        var existingInProgress = await _db.ExamAttempts
            .AnyAsync(a => a.ExamId == id && a.StudentUserId == studentId && a.Status == "InProgress");
        if (existingInProgress)
            return BadRequest(new { errors = new[] { "You already have an in-progress attempt for this exam." } });

        // Check max attempts
        if (exam.MaxAttempts.HasValue)
        {
            var attemptCount = await _db.ExamAttempts
                .CountAsync(a => a.ExamId == id && a.StudentUserId == studentId);
            if (attemptCount >= exam.MaxAttempts.Value)
                return BadRequest(new { errors = new[] { $"Maximum attempts ({exam.MaxAttempts.Value}) reached." } });
        }

        var attempt = ExamAttempt.Start(id, studentId);
        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        // Return questions (shuffled if exam requires)
        var questions = exam.Questions.OrderBy(q => q.SortOrder).ToList();
        if (exam.ShuffleQuestions)
        {
            var rng = new Random();
            questions = questions.OrderBy(_ => rng.Next()).ToList();
        }

        var questionDtos = questions.Select(q =>
        {
            List<object>? sanitizedOptions = null;
            if (!string.IsNullOrEmpty(q.OptionsJson))
            {
                try
                {
                    var options = JsonSerializer.Deserialize<List<QuestionOptionDto>>(q.OptionsJson, JsonOptions);
                    if (options != null)
                        sanitizedOptions = options.Select(o => (object)new { o.Key, o.Text }).ToList();
                }
                catch { }
            }

            return new
            {
                q.Id,
                q.QuestionText,
                q.QuestionType,
                q.ImageUrl,
                q.Points,
                q.SortOrder,
                Options = sanitizedOptions
            };
        }).ToList();

        return Ok(new
        {
            attemptId = attempt.Id,
            examId = exam.Id,
            examTitle = exam.Title,
            durationMinutes = exam.DurationMinutes,
            startedAt = attempt.StartedAt,
            questions = questionDtos
        });
    }

    /// <summary>
    /// Submit answers and grade an attempt (Student only)
    /// </summary>
    [HttpPost("attempts/{attemptId:guid}/submit")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> SubmitAttempt(Guid attemptId, [FromBody] SubmitExamRequest request)
    {
        var studentId = _currentUser.UserId!.Value;

        var attempt = await _db.ExamAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentUserId == studentId);
        if (attempt == null) return NotFound(new { errors = new[] { "Attempt not found." } });

        if (attempt.Status != "InProgress")
            return BadRequest(new { errors = new[] { "This attempt has already been completed." } });

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        // Check if timed out
        var deadline = attempt.StartedAt.AddMinutes(exam.DurationMinutes);
        var isTimedOut = DateTime.UtcNow > deadline.AddMinutes(1); // 1 minute grace

        // Load all questions for this exam
        var questions = await _db.ExamQuestions
            .Where(q => q.ExamId == exam.Id)
            .ToListAsync();

        var questionMap = questions.ToDictionary(q => q.Id);

        int totalPoints = questions.Sum(q => q.Points);
        int earnedPoints = 0;
        var answers = new List<ExamAnswer>();

        foreach (var submittedAnswer in request.Answers)
        {
            if (!questionMap.TryGetValue(submittedAnswer.QuestionId, out var question))
                continue;

            string? selectedOptionsJson = null;
            if (submittedAnswer.SelectedOptions != null && submittedAnswer.SelectedOptions.Any())
                selectedOptionsJson = JsonSerializer.Serialize(submittedAnswer.SelectedOptions, JsonOptions);

            var answer = ExamAnswer.Create(
                attemptId,
                submittedAnswer.QuestionId,
                submittedAnswer.AnswerText,
                selectedOptionsJson);

            // Grade based on question type
            var (isCorrect, points) = GradeAnswer(question, submittedAnswer);
            answer.Grade(isCorrect, points);
            earnedPoints += points;

            answers.Add(answer);
        }

        _db.ExamAnswers.AddRange(answers);

        if (isTimedOut)
            attempt.TimeOut(totalPoints, earnedPoints, exam.PassingScore);
        else
            attempt.Complete(totalPoints, earnedPoints, exam.PassingScore);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            attemptId = attempt.Id,
            status = attempt.Status,
            totalPoints = attempt.TotalPoints,
            earnedPoints = attempt.EarnedPoints,
            scorePercentage = attempt.ScorePercentage,
            passed = attempt.Passed,
            completedAt = attempt.CompletedAt
        });
    }

    /// <summary>
    /// Get attempt result with correct/incorrect breakdown
    /// </summary>
    [HttpGet("attempts/{attemptId:guid}/result")]
    public async Task<IActionResult> GetAttemptResult(Guid attemptId)
    {
        var userId = _currentUser.UserId!.Value;

        var attempt = await _db.ExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt == null) return NotFound(new { errors = new[] { "Attempt not found." } });

        // Allow access for the student who took it or the mentor who owns the exam
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId);
        if (exam == null) return NotFound(new { errors = new[] { "Exam not found." } });

        if (attempt.StudentUserId != userId && exam.MentorUserId != userId)
            return Forbid();

        if (attempt.Status == "InProgress")
            return BadRequest(new { errors = new[] { "This attempt is still in progress." } });

        // Load questions
        var questions = await _db.ExamQuestions
            .Where(q => q.ExamId == exam.Id)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        var answerMap = attempt.Answers.ToDictionary(a => a.QuestionId);

        var questionResults = questions.Select(q =>
        {
            answerMap.TryGetValue(q.Id, out var answer);

            object? studentAnswer = null;
            object? correctInfo = null;

            if (answer != null)
            {
                studentAnswer = new
                {
                    answerText = answer.AnswerText,
                    selectedOptionsJson = answer.SelectedOptionsJson,
                    isCorrect = answer.IsCorrect,
                    pointsEarned = answer.PointsEarned
                };
            }

            // Only show correct answers if exam allows
            if (exam.ShowResults)
            {
                correctInfo = new
                {
                    correctAnswer = q.CorrectAnswer,
                    optionsJson = q.OptionsJson,
                    explanation = q.Explanation
                };
            }

            return new
            {
                questionId = q.Id,
                questionText = q.QuestionText,
                questionType = q.QuestionType,
                points = q.Points,
                imageUrl = q.ImageUrl,
                studentAnswer,
                correctInfo
            };
        }).ToList();

        // Resolve student name
        var studentName = await _db.Users
            .Where(u => u.Id == attempt.StudentUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            attemptId = attempt.Id,
            examId = exam.Id,
            examTitle = exam.Title,
            studentUserId = attempt.StudentUserId,
            studentName = studentName ?? "?",
            startedAt = attempt.StartedAt,
            completedAt = attempt.CompletedAt,
            status = attempt.Status,
            totalPoints = attempt.TotalPoints,
            earnedPoints = attempt.EarnedPoints,
            scorePercentage = attempt.ScorePercentage,
            passed = attempt.Passed,
            showResults = exam.ShowResults,
            questions = questionResults
        });
    }

    /// <summary>
    /// List student's own attempts
    /// </summary>
    [HttpGet("my-attempts")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> GetMyAttempts(
        [FromQuery] Guid? examId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var studentId = _currentUser.UserId!.Value;

        var query = _db.ExamAttempts
            .Where(a => a.StudentUserId == studentId)
            .AsQueryable();

        if (examId.HasValue)
            query = query.Where(a => a.ExamId == examId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.ExamId,
                ExamTitle = a.Exam.Title,
                a.StartedAt,
                a.CompletedAt,
                a.Status,
                a.TotalPoints,
                a.EarnedPoints,
                a.ScorePercentage,
                a.Passed
            })
            .ToListAsync();

        return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    // ─── Admin Endpoints ────────────────────────────────────────────────

    /// <summary>
    /// List all exams (Admin only) with pagination and filters.
    /// </summary>
    [HttpGet("admin/all")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> GetAllExamsAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? scopeType = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Exams.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.Title.Contains(search));

        if (!string.IsNullOrWhiteSpace(scopeType))
            query = query.Where(e => e.ScopeType == scopeType);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var exams = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get mentor names
        var mentorIds = exams.Select(e => e.MentorUserId).Distinct().ToList();
        var mentorNames = await _db.Users.AsNoTracking()
            .Where(u => mentorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        // Get question counts and attempt stats per exam
        var examIds = exams.Select(e => e.Id).ToList();
        var questionCounts = await _db.ExamQuestions.AsNoTracking()
            .Where(q => examIds.Contains(q.ExamId))
            .GroupBy(q => q.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ExamId, g => g.Count);

        var attemptStats = await _db.ExamAttempts.AsNoTracking()
            .Where(a => examIds.Contains(a.ExamId) && a.Status == "Completed")
            .GroupBy(a => a.ExamId)
            .Select(g => new
            {
                ExamId = g.Key,
                Count = g.Count(),
                AvgScore = g.Average(a => (double?)a.ScorePercentage)
            })
            .ToDictionaryAsync(g => g.ExamId);

        var items = exams.Select(e =>
        {
            mentorNames.TryGetValue(e.MentorUserId, out var mentorName);
            questionCounts.TryGetValue(e.Id, out var qCount);
            attemptStats.TryGetValue(e.Id, out var aStat);

            return new
            {
                e.Id,
                e.Title,
                e.Description,
                e.MentorUserId,
                MentorName = mentorName ?? "?",
                e.ScopeType,
                e.ScopeId,
                e.DurationMinutes,
                e.PassingScore,
                e.IsPublished,
                QuestionCount = qCount,
                AttemptCount = aStat?.Count ?? 0,
                AverageScore = aStat?.AvgScore,
                e.CreatedAt
            };
        }).ToList();

        return Ok(new { items, totalCount, page, pageSize, totalPages });
    }

    // ─── Grading Logic ───────────────────────────────────────────────────

    private (bool isCorrect, int points) GradeAnswer(ExamQuestion question, SubmitAnswerDto submittedAnswer)
    {
        switch (question.QuestionType)
        {
            case "SingleChoice":
            case "TrueFalse":
                return GradeSingleChoice(question, submittedAnswer);

            case "MultipleChoice":
                return GradeMultipleChoice(question, submittedAnswer);

            case "ShortAnswer":
                return GradeShortAnswer(question, submittedAnswer);

            case "Essay":
                // Essay cannot be auto-graded
                return (false, 0);

            default:
                return (false, 0);
        }
    }

    private (bool isCorrect, int points) GradeSingleChoice(ExamQuestion question, SubmitAnswerDto submittedAnswer)
    {
        if (string.IsNullOrEmpty(question.OptionsJson))
            return (false, 0);

        try
        {
            var options = JsonSerializer.Deserialize<List<QuestionOptionDto>>(question.OptionsJson, JsonOptions);
            if (options == null) return (false, 0);

            var correctOption = options.FirstOrDefault(o => o.IsCorrect);
            if (correctOption == null) return (false, 0);

            var selected = submittedAnswer.SelectedOptions?.FirstOrDefault();
            if (selected == null) return (false, 0);

            var isCorrect = string.Equals(selected, correctOption.Key, StringComparison.OrdinalIgnoreCase);
            return (isCorrect, isCorrect ? question.Points : 0);
        }
        catch
        {
            return (false, 0);
        }
    }

    private (bool isCorrect, int points) GradeMultipleChoice(ExamQuestion question, SubmitAnswerDto submittedAnswer)
    {
        if (string.IsNullOrEmpty(question.OptionsJson))
            return (false, 0);

        try
        {
            var options = JsonSerializer.Deserialize<List<QuestionOptionDto>>(question.OptionsJson, JsonOptions);
            if (options == null) return (false, 0);

            var correctKeys = options.Where(o => o.IsCorrect).Select(o => o.Key.ToUpperInvariant()).OrderBy(k => k).ToList();
            var selectedKeys = (submittedAnswer.SelectedOptions ?? new List<string>())
                .Select(k => k.ToUpperInvariant()).OrderBy(k => k).ToList();

            if (!selectedKeys.Any()) return (false, 0);

            // Check for any wrong selections
            var wrongSelections = selectedKeys.Except(correctKeys).Any();
            if (wrongSelections) return (false, 0);

            // Exact match = full points
            if (correctKeys.SequenceEqual(selectedKeys))
                return (true, question.Points);

            // Partial credit: proportion of correct answers selected (no wrong ones)
            var partialPoints = (int)Math.Round((double)selectedKeys.Count / correctKeys.Count * question.Points);
            return (false, partialPoints);
        }
        catch
        {
            return (false, 0);
        }
    }

    private (bool isCorrect, int points) GradeShortAnswer(ExamQuestion question, SubmitAnswerDto submittedAnswer)
    {
        if (string.IsNullOrEmpty(question.CorrectAnswer))
            return (false, 0);

        var studentAnswer = submittedAnswer.AnswerText?.Trim();
        if (string.IsNullOrEmpty(studentAnswer))
            return (false, 0);

        var isCorrect = string.Equals(studentAnswer, question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
        return (isCorrect, isCorrect ? question.Points : 0);
    }
}

// ─── DTOs ──────────────────────────────────────────────────────────────────

public class CreateExamRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ScopeType { get; set; } = "General";
    public Guid? ScopeId { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int PassingScore { get; set; } = 50;
    public bool ShuffleQuestions { get; set; }
    public bool ShowResults { get; set; } = true;
    public int? MaxAttempts { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpdateExamRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int PassingScore { get; set; } = 50;
    public bool ShuffleQuestions { get; set; }
    public bool ShowResults { get; set; } = true;
    public int? MaxAttempts { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class AddQuestionRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "SingleChoice";
    public string? ImageUrl { get; set; }
    public int Points { get; set; } = 10;
    public int SortOrder { get; set; }
    public List<QuestionOptionDto>? Options { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Explanation { get; set; }
}

public class QuestionOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public class SubmitExamRequest
{
    public List<SubmitAnswerDto> Answers { get; set; } = new();
}

public class SubmitAnswerDto
{
    public Guid QuestionId { get; set; }
    public string? AnswerText { get; set; }
    public List<string>? SelectedOptions { get; set; }
}
