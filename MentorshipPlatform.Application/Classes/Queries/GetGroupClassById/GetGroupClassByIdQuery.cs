using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Queries.GetGroupClassById;

public record GroupClassDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    string? CoverImageUrl,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    int EnrolledCount,
    decimal PricePerSeat,
    string Currency,
    string Status,
    string MentorName,
    string? MentorAvatar,
    Guid MentorUserId,
    List<EnrollmentDto>? Enrollments);

public record EnrollmentDto(
    Guid Id,
    Guid StudentUserId,
    string StudentName,
    string? StudentAvatar,
    string Status,
    DateTime CreatedAt);

public record GetGroupClassByIdQuery(Guid ClassId) : IRequest<Result<GroupClassDetailDto>>;

public class GetGroupClassByIdQueryHandler
    : IRequestHandler<GetGroupClassByIdQuery, Result<GroupClassDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGroupClassByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<GroupClassDetailDto>> Handle(
        GetGroupClassByIdQuery request,
        CancellationToken cancellationToken)
    {
        var groupClass = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (groupClass == null)
            return Result<GroupClassDetailDto>.Failure("Grup dersi bulunamadı");

        var mentor = await _context.Users
            .Where(u => u.Id == groupClass.MentorUserId)
            .Select(u => new { u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        var enrolledCount = groupClass.Enrollments.Count(e =>
            e.Status == EnrollmentStatus.Confirmed ||
            e.Status == EnrollmentStatus.Attended);

        // Only mentor and admin can see enrollment list
        List<EnrollmentDto>? enrollments = null;
        var userId = _currentUser.UserId;
        if (userId.HasValue && userId.Value == groupClass.MentorUserId)
        {
            var studentIds = groupClass.Enrollments
                .Where(e => e.Status != EnrollmentStatus.Cancelled)
                .Select(e => e.StudentUserId)
                .ToList();

            var students = await _context.Users
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
                .ToDictionaryAsync(u => u.Id, cancellationToken);

            enrollments = groupClass.Enrollments
                .Where(e => e.Status != EnrollmentStatus.Cancelled)
                .Select(e =>
                {
                    var student = students.GetValueOrDefault(e.StudentUserId);
                    return new EnrollmentDto(
                        e.Id,
                        e.StudentUserId,
                        student?.DisplayName ?? "Student",
                        student?.AvatarUrl,
                        e.Status.ToString(),
                        e.CreatedAt);
                })
                .OrderBy(e => e.CreatedAt)
                .ToList();
        }

        // Virtual status: if Published but end time passed → "Expired"
        var displayStatus = groupClass.Status == ClassStatus.Published && groupClass.EndAt < DateTime.UtcNow
            ? "Expired"
            : groupClass.Status.ToString();

        return Result<GroupClassDetailDto>.Success(new GroupClassDetailDto(
            groupClass.Id,
            groupClass.Title,
            groupClass.Description,
            groupClass.Category,
            groupClass.CoverImageUrl,
            groupClass.StartAt,
            groupClass.EndAt,
            groupClass.Capacity,
            enrolledCount,
            groupClass.PricePerSeat,
            groupClass.Currency,
            displayStatus,
            mentor?.DisplayName ?? "Mentor",
            mentor?.AvatarUrl,
            groupClass.MentorUserId,
            enrollments));
    }
}
