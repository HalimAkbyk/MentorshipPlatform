using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Persistence;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/education")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminEducationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public AdminEducationController(ApplicationDbContext db) => _db = db;

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
            Sections = course.Sections.Select(s => new
            {
                s.Id,
                s.Title,
                s.SortOrder,
                Lectures = s.Lectures.Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.DurationSec,
                    l.IsPreview,
                    l.SortOrder,
                })
            }),
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
        };

        return Ok(result);
    }
}
