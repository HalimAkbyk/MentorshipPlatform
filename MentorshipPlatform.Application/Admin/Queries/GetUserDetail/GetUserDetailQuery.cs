using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetUserDetail;

// DTOs
public class MentorProfileSummaryDto
{
    public string University { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int? GraduationYear { get; set; }
    public bool IsListed { get; set; }
    public bool IsApprovedForBookings { get; set; }
    public int OfferingCount { get; set; }
    public int CompletedSessionCount { get; set; }
    public decimal TotalEarned { get; set; }
}

public class UserDetailDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public int? BirthYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string? ExternalProvider { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int BookingCount { get; set; }
    public int CompletedBookingCount { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public int CourseEnrollmentCount { get; set; }
    public int ClassEnrollmentCount { get; set; }
    public int ReviewCount { get; set; }
    public double? AverageRating { get; set; }
    public MentorProfileSummaryDto? MentorProfile { get; set; }
}

// Query
public record GetUserDetailQuery(Guid UserId) : IRequest<Result<UserDetailDto>>;

// Handler
public class GetUserDetailQueryHandler
    : IRequestHandler<GetUserDetailQuery, Result<UserDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUserDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UserDetailDto>> Handle(
        GetUserDetailQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
            return Result<UserDetailDto>.Failure("User not found.");

        // Booking counts (as student)
        var bookingCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(b => b.StudentUserId == request.UserId, cancellationToken);

        var completedBookingCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(b => b.StudentUserId == request.UserId
                             && b.Status == BookingStatus.Completed, cancellationToken);

        // Order counts and total spent
        var orderCount = await _context.Orders
            .AsNoTracking()
            .CountAsync(o => o.BuyerUserId == request.UserId, cancellationToken);

        var totalSpent = await _context.Orders
            .AsNoTracking()
            .Where(o => o.BuyerUserId == request.UserId && o.Status == OrderStatus.Paid)
            .SumAsync(o => (decimal?)o.AmountTotal, cancellationToken) ?? 0m;

        // Course enrollment count
        var courseEnrollmentCount = await _context.CourseEnrollments
            .AsNoTracking()
            .CountAsync(ce => ce.StudentUserId == request.UserId, cancellationToken);

        // Class enrollment count
        var classEnrollmentCount = await _context.ClassEnrollments
            .AsNoTracking()
            .CountAsync(ce => ce.StudentUserId == request.UserId, cancellationToken);

        // Reviews (as mentor: where MentorUserId == userId)
        var reviewCount = await _context.Reviews
            .AsNoTracking()
            .CountAsync(r => r.MentorUserId == request.UserId, cancellationToken);

        double? averageRating = null;
        if (reviewCount > 0)
        {
            averageRating = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.MentorUserId == request.UserId)
                .AverageAsync(r => (double)r.Rating, cancellationToken);
        }

        // Mentor profile
        MentorProfileSummaryDto? mentorProfileDto = null;
        if (user.Roles.Contains(UserRole.Mentor))
        {
            var mentorProfile = await _context.MentorProfiles
                .AsNoTracking()
                .Include(mp => mp.Verifications)
                .FirstOrDefaultAsync(mp => mp.UserId == request.UserId, cancellationToken);

            if (mentorProfile != null)
            {
                var offeringCount = await _context.Offerings
                    .AsNoTracking()
                    .CountAsync(o => o.MentorUserId == request.UserId, cancellationToken);

                var completedSessionCount = await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(b => b.MentorUserId == request.UserId
                                     && b.Status == BookingStatus.Completed, cancellationToken);

                var totalEarned = await _context.LedgerEntries
                    .AsNoTracking()
                    .Where(e => e.AccountType == LedgerAccountType.MentorAvailable
                                && e.Direction == LedgerDirection.Credit
                                && e.AccountOwnerUserId == request.UserId)
                    .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

                mentorProfileDto = new MentorProfileSummaryDto
                {
                    University = mentorProfile.University,
                    Department = mentorProfile.Department,
                    GraduationYear = mentorProfile.GraduationYear,
                    IsListed = mentorProfile.IsListed,
                    IsApprovedForBookings = mentorProfile.IsApprovedForBookings(),
                    OfferingCount = offeringCount,
                    CompletedSessionCount = completedSessionCount,
                    TotalEarned = totalEarned
                };
            }
        }

        var dto = new UserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Phone = user.Phone,
            BirthYear = user.BirthYear,
            Status = user.Status.ToString(),
            Roles = user.Roles.Select(r => r.ToString()).ToList(),
            ExternalProvider = user.ExternalProvider,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            BookingCount = bookingCount,
            CompletedBookingCount = completedBookingCount,
            OrderCount = orderCount,
            TotalSpent = totalSpent,
            CourseEnrollmentCount = courseEnrollmentCount,
            ClassEnrollmentCount = classEnrollmentCount,
            ReviewCount = reviewCount,
            AverageRating = averageRating,
            MentorProfile = mentorProfileDto
        };

        return Result<UserDetailDto>.Success(dto);
    }
}
