using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Admin.Queries.GetPendingMentors;

public record GetPendingMentorsQuery : IRequest<Result<List<PendingMentorDto>>>;

public class PendingMentorDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? University { get; set; }
    public string? Department { get; set; }
    public int? GraduationYear { get; set; }
    public string? Headline { get; set; }
    public string? Bio { get; set; }
    public decimal? HourlyRate { get; set; }
    public bool IsListed { get; set; }
    public DateTime CreatedAt { get; set; }

    // Onboarding profile info
    public string? City { get; set; }
    public string? EducationStatus { get; set; }
    public string? Categories { get; set; }
    public string? Subtopics { get; set; }
    public string? Languages { get; set; }
    public string? Certifications { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? SessionFormats { get; set; }

    public int OfferingCount { get; set; }
    public bool HasAvailability { get; set; }

    public List<VerificationDto> Verifications { get; set; } = new();
}

public class VerificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Notes { get; set; }
}

public class GetPendingMentorsQueryHandler
    : IRequestHandler<GetPendingMentorsQuery, Result<List<PendingMentorDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPendingMentorsQueryHandler> _logger;

    public GetPendingMentorsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetPendingMentorsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<PendingMentorDto>>> Handle(
        GetPendingMentorsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // All mentor profiles that are not listed (pending admin review)
            // OR have pending verifications
            var mentorProfiles = await _context.MentorProfiles
                .Include(m => m.User)
                .Include(m => m.Verifications)
                .Include(m => m.Offerings)
                .Where(m =>
                    !m.IsListed ||
                    m.Verifications.Any(v => v.Status == VerificationStatus.Pending)
                )
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(cancellationToken);

            var mentorUserIds = mentorProfiles.Select(m => m.UserId).ToList();

            // Batch load onboarding profiles
            var onboardings = await _context.MentorOnboardingProfiles
                .AsNoTracking()
                .Where(o => mentorUserIds.Contains(o.MentorUserId))
                .ToListAsync(cancellationToken);

            var onboardingMap = onboardings.ToDictionary(o => o.MentorUserId);

            // Check availability
            var mentorsWithAvailability = await _context.AvailabilitySlots
                .AsNoTracking()
                .Where(s => mentorUserIds.Contains(s.MentorUserId) && !s.IsBooked && s.StartAt > DateTime.UtcNow)
                .Select(s => s.MentorUserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var availabilitySet = mentorsWithAvailability.ToHashSet();

            var result = mentorProfiles.Select(m =>
            {
                onboardingMap.TryGetValue(m.UserId, out var ob);
                return new PendingMentorDto
                {
                    UserId = m.UserId,
                    FullName = m.User.DisplayName,
                    AvatarUrl = m.User.AvatarUrl,
                    Email = m.User.Email,
                    University = m.University,
                    Department = m.Department,
                    GraduationYear = m.GraduationYear,
                    Headline = m.Headline,
                    Bio = m.Bio,
                    HourlyRate = m.Offerings.FirstOrDefault()?.PriceAmount,
                    IsListed = m.IsListed,
                    CreatedAt = m.CreatedAt,
                    City = ob?.City,
                    EducationStatus = ob?.YearsOfExperience,
                    Categories = ob?.Categories,
                    Subtopics = ob?.Subtopics,
                    Languages = ob?.Languages,
                    Certifications = ob?.Certifications,
                    LinkedinUrl = ob?.LinkedinUrl,
                    GithubUrl = ob?.GithubUrl,
                    PortfolioUrl = ob?.PortfolioUrl,
                    SessionFormats = ob?.SessionFormats,
                    OfferingCount = m.Offerings.Count(o => o.IsActive),
                    HasAvailability = availabilitySet.Contains(m.UserId),
                    Verifications = m.Verifications
                        .OrderByDescending(v => v.CreatedAt)
                        .Select(v => new VerificationDto
                        {
                            Id = v.Id,
                            Type = v.Type.ToString(),
                            Status = v.Status.ToString(),
                            DocumentUrl = v.DocumentUrl,
                            SubmittedAt = v.CreatedAt,
                            ReviewedAt = v.ReviewedAt,
                            Notes = v.Notes
                        })
                        .ToList()
                };
            }).ToList();

            return Result<List<PendingMentorDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending mentors");
            return Result<List<PendingMentorDto>>.Failure("Failed to retrieve pending mentors");
        }
    }
}
