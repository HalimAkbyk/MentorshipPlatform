using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Courses.Commands.AddCourseAdminNote;
using MentorshipPlatform.Application.Courses.Commands.SuspendCourse;
using MentorshipPlatform.Application.Courses.Commands.ToggleLectureActive;
using MentorshipPlatform.Application.Courses.Commands.UnsuspendCourse;
using MentorshipPlatform.Application.Courses.Queries.GetCourseAdminNotes;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Persistence;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/education")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminEducationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;
    private readonly IStorageService _storage;

    public AdminEducationController(ApplicationDbContext db, IMediator mediator, IStorageService storage)
    {
        _db = db;
        _mediator = mediator;
        _storage = storage;
    }

    // GET /api/admin/education/bookings - All bookings (paginated, filtered)
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var query = _db.Bookings
            .Include(b => b.Offering)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var bs))
            query = query.Where(b => b.Status == bs);

        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate))
            query = query.Where(b => b.StartAt >= fromDate.ToUniversalTime());

        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate))
            query = query.Where(b => b.StartAt <= toDate.ToUniversalTime());

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(b => b.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.StudentUserId,
                b.MentorUserId,
                b.StartAt,
                b.DurationMin,
                Status = b.Status.ToString(),
                b.OfferingId,
                OfferingTitle = b.Offering != null ? b.Offering.Title : null,
                b.CreatedAt
            })
            .ToListAsync();

        // Resolve user names
        var userIds = items.SelectMany(i => new[] { i.StudentUserId, i.MentorUserId }).Distinct().ToList();
        var userNames = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var result = items.Select(b => new
        {
            b.Id,
            b.StudentUserId,
            StudentName = userNames.GetValueOrDefault(b.StudentUserId, "?"),
            b.MentorUserId,
            MentorName = userNames.GetValueOrDefault(b.MentorUserId, "?"),
            b.StartAt,
            b.DurationMin,
            b.Status,
            b.OfferingTitle,
            b.CreatedAt
        });

        return Ok(new { items = result, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    // GET /api/admin/education/group-classes - All group classes
    [HttpGet("group-classes")]
    public async Task<IActionResult> GetGroupClasses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = _db.GroupClasses.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(gc => gc.Category == category);

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<ClassStatus>(status, true, out var cs))
                query = query.Where(gc => gc.Status == cs);
        }

        if (!string.IsNullOrEmpty(search))
            query = query.Where(gc => gc.Title.ToLower().Contains(search.ToLower()));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(gc => gc.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(gc => new
            {
                gc.Id,
                gc.Title,
                gc.Category,
                gc.MentorUserId,
                gc.StartAt,
                gc.EndAt,
                gc.Capacity,
                gc.PricePerSeat,
                gc.Currency,
                Status = gc.Status.ToString(),
                gc.CreatedAt
            })
            .ToListAsync();

        // Compute enrolled counts from ClassEnrollments
        var classIds = items.Select(i => i.Id).ToList();
        var enrolledCounts = await _db.ClassEnrollments
            .Where(e => classIds.Contains(e.ClassId)
                && (e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.Attended))
            .GroupBy(e => e.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ClassId, g => g.Count);

        // Resolve mentor names
        var mentorIds = items.Select(i => i.MentorUserId).Distinct().ToList();
        var mentorNames = await _db.Users
            .Where(u => mentorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var result = items.Select(gc => new
        {
            gc.Id,
            gc.Title,
            gc.Category,
            gc.MentorUserId,
            MentorName = mentorNames.GetValueOrDefault(gc.MentorUserId, "?"),
            gc.StartAt,
            gc.EndAt,
            gc.Capacity,
            EnrolledCount = enrolledCounts.GetValueOrDefault(gc.Id, 0),
            gc.PricePerSeat,
            gc.Currency,
            gc.Status,
            gc.CreatedAt
        });

        return Ok(new { items = result, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    // GET /api/admin/education/courses - All video courses
    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = _db.Courses.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<CourseStatus>(status, true, out var cs))
                query = query.Where(c => c.Status == cs);
        }

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Title.ToLower().Contains(search.ToLower()));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.MentorUserId,
                c.Price,
                c.Currency,
                Status = c.Status.ToString(),
                Level = c.Level.ToString(),
                c.Category,
                c.EnrollmentCount,
                c.CreatedAt,
                c.UpdatedAt
            })
            .ToListAsync();

        // Resolve instructor/mentor names
        var mentorIds = items.Select(i => i.MentorUserId).Distinct().ToList();
        var mentorNames = await _db.Users
            .Where(u => mentorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var result = items.Select(c => new
        {
            c.Id,
            c.Title,
            c.MentorUserId,
            MentorName = mentorNames.GetValueOrDefault(c.MentorUserId, "?"),
            c.Price,
            c.Currency,
            c.Status,
            c.Level,
            c.Category,
            c.EnrollmentCount,
            c.CreatedAt
        });

        return Ok(new { items = result, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    // GET /api/admin/education/bookings/{id} - Booking detail
    [HttpGet("bookings/{id:guid}")]
    public async Task<IActionResult> GetBookingDetail(Guid id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Offering)
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
            return NotFound(new { errors = new[] { "Booking not found." } });

        // Get related order/payment info
        var order = await _db.Orders
            .Where(o => o.ResourceId == id && o.Type == Domain.Enums.OrderType.Booking)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.Id, o.AmountTotal, Status = o.Status.ToString(), o.CreatedAt })
            .FirstOrDefaultAsync();

        // Get session/video info
        var session = await _db.VideoSessions
            .Where(vs => vs.ResourceType == "Booking" && vs.ResourceId == id)
            .OrderByDescending(vs => vs.CreatedAt)
            .Select(vs => new { vs.Id, vs.RoomName, Status = vs.Status.ToString(), vs.CreatedAt })
            .FirstOrDefaultAsync();

        // Get messages count
        var messageCount = await _db.Messages
            .Where(m => m.BookingId == id)
            .CountAsync();

        var result = new
        {
            booking.Id,
            booking.StudentUserId,
            StudentName = booking.Student?.DisplayName ?? "?",
            StudentEmail = booking.Student?.Email ?? "",
            booking.MentorUserId,
            MentorName = booking.Mentor?.DisplayName ?? "?",
            MentorEmail = booking.Mentor?.Email ?? "",
            booking.StartAt,
            booking.EndAt,
            booking.DurationMin,
            Status = booking.Status.ToString(),
            booking.CancellationReason,
            booking.RescheduleCountStudent,
            booking.RescheduleCountMentor,
            booking.CreatedAt,
            OfferingId = booking.OfferingId,
            OfferingTitle = booking.Offering?.Title ?? "-",
            OfferingPrice = booking.Offering?.PriceAmount ?? 0,
            OfferingCurrency = booking.Offering?.Currency ?? "TRY",
            OfferingDuration = booking.Offering?.DurationMinDefault ?? 0,
            Order = order,
            Session = session,
            MessageCount = messageCount,
        };

        return Ok(result);
    }

    // GET /api/admin/education/group-classes/{id} - Group class detail
    [HttpGet("group-classes/{id:guid}")]
    public async Task<IActionResult> GetGroupClassDetail(Guid id)
    {
        var gc = await _db.GroupClasses
            .FirstOrDefaultAsync(c => c.Id == id);

        if (gc == null)
            return NotFound(new { errors = new[] { "Group class not found." } });

        // Mentor info
        var mentor = await _db.Users
            .Where(u => u.Id == gc.MentorUserId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync();

        // Enrollments
        var enrollments = await _db.ClassEnrollments
            .Where(e => e.ClassId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.StudentUserId,
                Status = e.Status.ToString(),
                e.CreatedAt,
            })
            .ToListAsync();

        // Resolve student names
        var studentIds = enrollments.Select(e => e.StudentUserId).Distinct().ToList();
        var studentNames = await _db.Users
            .Where(u => studentIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.Email });

        // Revenue from paid enrollment orders
        var enrollmentIds = enrollments.Select(e => e.Id).ToList();
        var totalRevenue = await _db.Orders
            .Where(o => o.Type == Domain.Enums.OrderType.GroupClass
                && enrollmentIds.Contains(o.ResourceId)
                && o.Status == Domain.Enums.OrderStatus.Paid)
            .SumAsync(o => o.AmountTotal);

        var confirmedCount = enrollments.Count(e => e.Status == "Confirmed" || e.Status == "Attended");

        var result = new
        {
            gc.Id,
            gc.Title,
            gc.Description,
            gc.Category,
            gc.CoverImageUrl,
            gc.MentorUserId,
            MentorName = mentor?.DisplayName ?? "?",
            MentorEmail = mentor?.Email ?? "",
            gc.StartAt,
            gc.EndAt,
            gc.Capacity,
            gc.PricePerSeat,
            gc.Currency,
            Status = gc.Status.ToString(),
            gc.CreatedAt,
            EnrolledCount = confirmedCount,
            TotalRevenue = totalRevenue,
            Enrollments = enrollments.Select(e => new
            {
                e.Id,
                e.StudentUserId,
                StudentName = studentNames.GetValueOrDefault(e.StudentUserId)?.DisplayName ?? "?",
                StudentEmail = studentNames.GetValueOrDefault(e.StudentUserId)?.Email ?? "",
                e.Status,
                e.CreatedAt,
            }),
        };

        return Ok(result);
    }

    // GET /api/admin/education/exams/{id} - Exam detail
    [HttpGet("exams/{id:guid}")]
    public async Task<IActionResult> GetExamDetail(Guid id)
    {
        var exam = await _db.Exams
            .Include(e => e.Questions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound(new { errors = new[] { "Exam not found." } });

        // Mentor info
        var mentor = await _db.Users
            .Where(u => u.Id == exam.MentorUserId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync();

        // Attempts
        var attempts = await _db.ExamAttempts
            .Where(a => a.ExamId == id)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new
            {
                a.Id,
                a.StudentUserId,
                a.ScorePercentage,
                a.EarnedPoints,
                a.TotalPoints,
                a.Passed,
                a.StartedAt,
                a.CompletedAt,
                a.Status,
            })
            .ToListAsync();

        // Resolve student names
        var studentIds = attempts.Select(a => a.StudentUserId).Distinct().ToList();
        var studentNames = await _db.Users
            .Where(u => studentIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.Email });

        var completedAttempts = attempts.Where(a => a.CompletedAt != null).ToList();
        var averageScore = completedAttempts.Count > 0 ? completedAttempts.Average(a => (double)a.ScorePercentage) : 0;
        var passRate = completedAttempts.Count > 0 ? (double)completedAttempts.Count(a => a.Passed) / completedAttempts.Count * 100 : 0;

        var result = new
        {
            exam.Id,
            exam.Title,
            exam.Description,
            exam.MentorUserId,
            MentorName = mentor?.DisplayName ?? "?",
            MentorEmail = mentor?.Email ?? "",
            exam.ScopeType,
            exam.ScopeId,
            exam.DurationMinutes,
            exam.PassingScore,
            exam.IsPublished,
            exam.ShuffleQuestions,
            exam.ShowResults,
            exam.MaxAttempts,
            exam.StartDate,
            exam.EndDate,
            exam.CreatedAt,
            QuestionCount = exam.Questions.Count,
            AttemptCount = attempts.Count,
            AverageScore = Math.Round(averageScore, 1),
            PassRate = Math.Round(passRate, 1),
            Questions = exam.Questions.Select(q => new
            {
                q.Id,
                q.QuestionText,
                q.QuestionType,
                q.Points,
                q.SortOrder,
            }).OrderBy(q => q.SortOrder),
            Attempts = attempts.Select(a => new
            {
                a.Id,
                a.StudentUserId,
                StudentName = studentNames.GetValueOrDefault(a.StudentUserId)?.DisplayName ?? "?",
                StudentEmail = studentNames.GetValueOrDefault(a.StudentUserId)?.Email ?? "",
                a.ScorePercentage,
                a.EarnedPoints,
                a.TotalPoints,
                a.Passed,
                a.Status,
                a.StartedAt,
                a.CompletedAt,
            }),
        };

        return Ok(result);
    }

    // GET /api/admin/education/courses/{id} - Course detail
    [HttpGet("courses/{id:guid}")]
    public async Task<IActionResult> GetCourseDetail(Guid id)
    {
        var course = await _db.Courses
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lectures.OrderBy(l => l.SortOrder))
            .Include(c => c.MentorUser)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
            return NotFound(new { errors = new[] { "Course not found." } });

        // Enrollments
        var enrollments = await _db.CourseEnrollments
            .Where(e => e.CourseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.StudentUserId,
                Status = e.Status.ToString(),
                Progress = e.CompletionPercentage,
                e.CreatedAt,
            })
            .ToListAsync();

        // Resolve student names
        var studentIds = enrollments.Select(e => e.StudentUserId).Distinct().ToList();
        var studentNames = await _db.Users
            .Where(u => studentIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.Email });

        // Revenue from paid orders for this course's enrollments
        var enrollmentIds = enrollments.Select(e => e.Id).ToList();
        var totalRevenue = await _db.Orders
            .Where(o => o.Type == Domain.Enums.OrderType.Course
                && enrollmentIds.Contains(o.ResourceId)
                && o.Status == Domain.Enums.OrderStatus.Paid)
            .SumAsync(o => o.AmountTotal);

        // Generate presigned video URLs for admin viewing
        var sectionsWithUrls = new List<object>();
        foreach (var section in course.Sections)
        {
            var lecturesWithUrls = new List<object>();
            foreach (var lecture in section.Lectures)
            {
                string? videoUrl = null;
                if (!string.IsNullOrEmpty(lecture.VideoKey))
                {
                    try { videoUrl = await _storage.GetPresignedUrlAsync(lecture.VideoKey, TimeSpan.FromHours(2)); }
                    catch { /* ignore storage errors */ }
                }

                lecturesWithUrls.Add(new
                {
                    lecture.Id,
                    lecture.Title,
                    lecture.DurationSec,
                    lecture.IsPreview,
                    lecture.IsActive,
                    lecture.SortOrder,
                    lecture.VideoKey,
                    VideoUrl = videoUrl,
                });
            }

            sectionsWithUrls.Add(new
            {
                section.Id,
                section.Title,
                section.SortOrder,
                Lectures = lecturesWithUrls,
            });
        }

        // Get admin notes (latest 50)
        var adminNotes = await _db.CourseAdminNotes
            .Where(n => n.CourseId == id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new
            {
                n.Id,
                n.LectureId,
                n.AdminUserId,
                AdminName = n.AdminUser.DisplayName ?? "Admin",
                NoteType = n.NoteType.ToString(),
                Flag = n.Flag.HasValue ? n.Flag.Value.ToString() : (string?)null,
                n.Content,
                n.LectureTitle,
                n.CreatedAt,
            })
            .ToListAsync();

        var result = new
        {
            course.Id,
            course.Title,
            course.Description,
            course.ShortDescription,
            course.CoverImageUrl,
            course.Price,
            course.Currency,
            Status = course.Status.ToString(),
            Level = course.Level.ToString(),
            course.Category,
            course.Language,
            course.TotalDurationSec,
            course.TotalLectures,
            course.RatingAvg,
            course.RatingCount,
            course.EnrollmentCount,
            course.CreatedAt,
            course.UpdatedAt,
            MentorUserId = course.MentorUserId,
            MentorName = course.MentorUser?.DisplayName ?? "?",
            MentorEmail = course.MentorUser?.Email ?? "",
            TotalRevenue = totalRevenue,
            Sections = sectionsWithUrls,
            Enrollments = enrollments.Select(e => new
            {
                e.Id,
                e.StudentUserId,
                StudentName = studentNames.GetValueOrDefault(e.StudentUserId)?.DisplayName ?? "?",
                StudentEmail = studentNames.GetValueOrDefault(e.StudentUserId)?.Email ?? "",
                e.Status,
                e.Progress,
                e.CreatedAt,
            }),
            AdminNotes = adminNotes,
        };

        return Ok(result);
    }

    // ========================================================================
    // Session Reports
    // ========================================================================

    private record BookingInfo(Guid StudentUserId, Guid MentorUserId, DateTime StartAt, DateTime? EndAt, int DurationMin);
    private record GroupClassInfo(string Title, Guid MentorUserId, DateTime StartAt, DateTime EndAt);

    // GET /api/admin/education/session-reports
    [HttpGet("session-reports")]
    public async Task<IActionResult> GetSessionReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var query = _db.VideoSessions
            .Include(vs => vs.Participants)
            .AsQueryable();

        // Type filter
        if (!string.IsNullOrEmpty(type) && type != "all")
        {
            if (type == "booking") query = query.Where(vs => vs.ResourceType == "Booking");
            else if (type == "group-class") query = query.Where(vs => vs.ResourceType == "GroupClass");
        }

        // Status filter
        if (!string.IsNullOrEmpty(status) && status != "all")
        {
            if (Enum.TryParse<VideoSessionStatus>(status, true, out var ss))
                query = query.Where(vs => vs.Status == ss);
        }

        // Date range filter
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate))
            query = query.Where(vs => vs.CreatedAt >= fromDate.ToUniversalTime());
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate))
            query = query.Where(vs => vs.CreatedAt <= toDate.ToUniversalTime());

        var totalCount = await query.CountAsync();

        var sessions = await query
            .OrderByDescending(vs => vs.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Collect resource IDs by type
        var bookingIds = sessions
            .Where(s => s.ResourceType == "Booking" && s.ResourceId != Guid.Empty)
            .Select(s => s.ResourceId).Distinct().ToList();

        var gcIds = sessions
            .Where(s => s.ResourceType == "GroupClass")
            .Select(s => s.RoomName.Replace("group-class-", ""))
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id))
            .Distinct().ToList();

        // Fetch bookings
        var bookings = bookingIds.Count > 0
            ? await _db.Bookings
                .Where(b => bookingIds.Contains(b.Id))
                .ToDictionaryAsync(
                    b => b.Id,
                    b => new BookingInfo(b.StudentUserId, b.MentorUserId, b.StartAt, b.EndAt, b.DurationMin))
            : new Dictionary<Guid, BookingInfo>();

        // Fetch group classes
        var groupClasses = gcIds.Count > 0
            ? await _db.GroupClasses
                .Where(gc => gcIds.Contains(gc.Id))
                .ToDictionaryAsync(
                    gc => gc.Id,
                    gc => new GroupClassInfo(gc.Title, gc.MentorUserId, gc.StartAt, gc.EndAt))
            : new Dictionary<Guid, GroupClassInfo>();

        // Collect all user IDs for name resolution
        var allUserIds = new HashSet<Guid>();
        foreach (var s in sessions)
            foreach (var p in s.Participants)
                allUserIds.Add(p.UserId);
        foreach (var b in bookings.Values)
        {
            allUserIds.Add(b.StudentUserId);
            allUserIds.Add(b.MentorUserId);
        }
        foreach (var gc in groupClasses.Values)
            allUserIds.Add(gc.MentorUserId);

        var userNames = allUserIds.Count > 0
            ? await _db.Users
                .Where(u => allUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName)
            : new Dictionary<Guid, string>();

        // Build result items
        var items = sessions.Select(s =>
        {
            string title = s.RoomName;
            string mentorName = "?";
            var mentorUserId = Guid.Empty;
            DateTime? scheduledStart = null;
            DateTime? scheduledEnd = null;

            if (s.ResourceType == "Booking" && s.ResourceId != Guid.Empty && bookings.TryGetValue(s.ResourceId, out var bk))
            {
                var studentName = userNames.GetValueOrDefault(bk.StudentUserId, "?");
                mentorName = userNames.GetValueOrDefault(bk.MentorUserId, "?");
                mentorUserId = bk.MentorUserId;
                title = $"1:1 Ders - {studentName} & {mentorName}";
                scheduledStart = bk.StartAt;
                scheduledEnd = bk.EndAt;
            }
            else if (s.ResourceType == "GroupClass")
            {
                var gcIdStr = s.RoomName.Replace("group-class-", "");
                if (Guid.TryParse(gcIdStr, out var gcId) && groupClasses.TryGetValue(gcId, out var gc))
                {
                    title = gc.Title;
                    mentorName = userNames.GetValueOrDefault(gc.MentorUserId, "?");
                    mentorUserId = gc.MentorUserId;
                    scheduledStart = gc.StartAt;
                    scheduledEnd = gc.EndAt;
                }
            }

            var participants = s.Participants.Select(p =>
            {
                var dur = p.DurationSec;
                var mins = dur / 60;
                var secs = dur % 60;
                var isMentor = p.UserId == mentorUserId;
                return new
                {
                    p.UserId,
                    DisplayName = userNames.GetValueOrDefault(p.UserId, "?"),
                    Role = isMentor ? "Mentor" : "Student",
                    p.JoinedAt,
                    LeftAt = p.LeftAt.HasValue ? (DateTime?)p.LeftAt.Value : null,
                    p.DurationSec,
                    DurationFormatted = $"{mins:D2}:{secs:D2}"
                };
            }).OrderByDescending(p => p.Role == "Mentor").ThenBy(p => p.JoinedAt).ToList();

            return new
            {
                SessionId = s.Id,
                s.ResourceType,
                s.ResourceId,
                s.RoomName,
                SessionStatus = s.Status.ToString(),
                SessionCreatedAt = s.CreatedAt,
                Title = title,
                MentorName = mentorName,
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                ParticipantCount = participants.Count,
                TotalDurationSec = s.GetTotalDurationSeconds(),
                Participants = participants
            };
        }).ToList();

        // Apply search filter (post-fetch, on resolved names)
        if (!string.IsNullOrEmpty(search))
        {
            var lowerSearch = search.ToLower();
            items = items.Where(i =>
                i.Title.ToLower().Contains(lowerSearch) ||
                i.MentorName.ToLower().Contains(lowerSearch) ||
                i.Participants.Any(p => p.DisplayName.ToLower().Contains(lowerSearch))
            ).ToList();
            totalCount = items.Count;
        }

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    // ========================================================================
    // Course Content Moderation Endpoints
    // ========================================================================

    // POST /api/admin/education/courses/{courseId}/suspend
    [HttpPost("courses/{courseId:guid}/suspend")]
    public async Task<IActionResult> SuspendCourse(Guid courseId, [FromBody] SuspendCourseRequest body)
    {
        var result = await _mediator.Send(new SuspendCourseCommand(courseId, body.Reason));
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    // POST /api/admin/education/courses/{courseId}/unsuspend
    [HttpPost("courses/{courseId:guid}/unsuspend")]
    public async Task<IActionResult> UnsuspendCourse(Guid courseId, [FromBody] UnsuspendCourseRequest? body)
    {
        var result = await _mediator.Send(new UnsuspendCourseCommand(courseId, body?.Note));
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    // POST /api/admin/education/courses/{courseId}/lectures/{lectureId}/toggle-active
    [HttpPost("courses/{courseId:guid}/lectures/{lectureId:guid}/toggle-active")]
    public async Task<IActionResult> ToggleLectureActive(Guid courseId, Guid lectureId, [FromBody] ToggleLectureActiveRequest body)
    {
        var result = await _mediator.Send(new ToggleLectureActiveCommand(courseId, lectureId, body.IsActive, body.Reason));
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    // POST /api/admin/education/courses/{courseId}/notes
    [HttpPost("courses/{courseId:guid}/notes")]
    public async Task<IActionResult> AddCourseAdminNote(Guid courseId, [FromBody] AddCourseAdminNoteRequest body)
    {
        LectureReviewFlag? flag = null;
        if (!string.IsNullOrEmpty(body.Flag) && Enum.TryParse<LectureReviewFlag>(body.Flag, true, out var parsed))
            flag = parsed;

        var result = await _mediator.Send(new AddCourseAdminNoteCommand(courseId, body.LectureId, flag, body.Content));
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    // GET /api/admin/education/courses/{courseId}/notes
    [HttpGet("courses/{courseId:guid}/notes")]
    public async Task<IActionResult> GetCourseAdminNotes(Guid courseId)
    {
        var result = await _mediator.Send(new GetCourseAdminNotesQuery(courseId));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    // Request DTOs
    public record SuspendCourseRequest(string Reason);
    public record UnsuspendCourseRequest(string? Note);
    public record ToggleLectureActiveRequest(bool IsActive, string? Reason);
    public record AddCourseAdminNoteRequest(Guid? LectureId, string? Flag, string Content);
}
