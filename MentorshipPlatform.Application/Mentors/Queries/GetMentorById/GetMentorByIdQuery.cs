using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Mentors.Queries.GetMentorById;

public record GetMentorByIdQuery(Guid MentorUserId) : IRequest<Result<MentorDetailDto>>;

public record MentorDetailDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Bio,
    string University,
    string Department,
    int? GraduationYear,
    string? Headline,
    decimal RatingAvg,
    int RatingCount,
    bool IsListed,
    bool IsOwnProfile,
    string? VerificationStatus,
    List<OfferingDto> Offerings,
    List<VerificationBadgeDto> Badges,
    List<AvailabilitySlotDto> AvailableSlots);

public record OfferingDto(
    Guid Id,
    OfferingType Type,
    string Title,
    string? Description,
    int DurationMin,
    decimal Price,
    string Currency);

public record VerificationBadgeDto(VerificationType Type, bool IsVerified);

public record AvailabilitySlotDto(Guid Id, DateTime StartAt, DateTime EndAt);

public class GetMentorByIdQueryHandler
    : IRequestHandler<GetMentorByIdQuery, Result<MentorDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MentorDetailDto>> Handle(
        GetMentorByIdQuery request,
        CancellationToken cancellationToken)
    {
        var mentor = await _context.MentorProfiles
            .Include(m => m.User)
            .Include(m => m.Offerings)
            .Include(m => m.Verifications)
            .FirstOrDefaultAsync(m => m.UserId == request.MentorUserId, cancellationToken);

        // Kendi profilini görüntüleyen mentor IsListed kontrolünden muaf
        var isOwnProfile = _currentUser.UserId.HasValue && _currentUser.UserId.Value == request.MentorUserId;
        if (mentor == null)
            return Result<MentorDetailDto>.Failure("Mentor not found");
        if (!mentor.IsListed && !isOwnProfile)
            return Result<MentorDetailDto>.Failure("Mentor not found");

        // Get available slots (next 30 days)
        var now = DateTime.UtcNow;
        var availableSlots = await _context.AvailabilitySlots
            .Where(s => 
                s.MentorUserId == request.MentorUserId &&
                !s.IsBooked &&
                s.StartAt >= now &&
                s.StartAt <= now.AddDays(30))
            .OrderBy(s => s.StartAt)
            .Take(50)
            .Select(s => new AvailabilitySlotDto(s.Id, s.StartAt, s.EndAt))
            .ToListAsync(cancellationToken);

        var offerings = mentor.Offerings
            .Where(o => o.IsActive)
            .Select(o => new OfferingDto(
                o.Id,
                o.Type,
                o.Title,
                o.Description,
                o.DurationMinDefault,
                o.PriceAmount,
                o.Currency))
            .ToList();

        var badges = Enum.GetValues<VerificationType>()
            .Select(type => new VerificationBadgeDto(type, mentor.IsVerified(type)))
            .ToList();

        // Doğrulama durumu özeti (sadece kendi profilinde anlamlı)
        string? verificationStatus = null;
        if (isOwnProfile)
        {
            var verifications = mentor.Verifications.ToList();
            if (!verifications.Any())
            {
                verificationStatus = "NoDocuments"; // Hiç belge yüklenmemiş
            }
            else if (verifications.Any(v => v.Status == VerificationStatus.Approved))
            {
                verificationStatus = "Approved"; // En az bir belge onaylı
            }
            else if (verifications.Any(v => v.Status == VerificationStatus.Pending))
            {
                verificationStatus = "PendingApproval"; // Onay bekliyor
            }
            else if (verifications.All(v => v.Status == VerificationStatus.Rejected))
            {
                verificationStatus = "Rejected"; // Hepsi reddedilmiş
            }
        }

        var dto = new MentorDetailDto(
            mentor.UserId,
            mentor.User.DisplayName,
            mentor.User.AvatarUrl,
            mentor.Bio,
            mentor.University,
            mentor.Department,
            mentor.GraduationYear,
            mentor.Headline,
            mentor.RatingAvg,
            mentor.RatingCount,
            mentor.IsListed,
            isOwnProfile,
            verificationStatus,
            offerings,
            badges,
            availableSlots);

        return Result<MentorDetailDto>.Success(dto);
    }
}
