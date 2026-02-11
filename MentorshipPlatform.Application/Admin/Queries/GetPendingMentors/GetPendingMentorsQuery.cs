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
    public string Email { get; set; } = string.Empty;
    public string? University { get; set; }
    public string? Department { get; set; }
    public int? GraduationYear { get; set; }
    public string? Headline { get; set; }
    public string? Bio { get; set; }
    public decimal? HourlyRate { get; set; }
    public bool IsListed { get; set; }
    public DateTime CreatedAt { get; set; }
    
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
            // ✅ Onay bekleyen mentörleri getir
            // - En az 1 pending verification var
            // - VEYA IsListed = false (ama verifications approved)
            var mentors = await _context.MentorProfiles
                .Include(m => m.User)
                .Include(m => m.Verifications)
                .Include(m => m.Offerings)
                .Where(m => 
                    // Pending verification var
                    m.Verifications.Any(v => v.Status == VerificationStatus.Pending)
                    ||
                    // Veya: Tüm verifications approved ama IsListed = false
                    (!m.IsListed && m.Verifications.Any() && 
                     m.Verifications.All(v => v.Status == VerificationStatus.Approved))
                )
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new PendingMentorDto
                {
                    UserId = m.UserId,
                    FullName = m.User.DisplayName,
                    Email = m.User.Email,
                    University = m.University,
                    Department = m.Department,
                    GraduationYear = m.GraduationYear,
                    Headline = m.Headline,
                    Bio = m.Bio,
                    HourlyRate = m.Offerings.FirstOrDefault() != null 
                        ? m.Offerings.FirstOrDefault()!.PriceAmount 
                        : null,
                    IsListed = m.IsListed,
                    CreatedAt = m.CreatedAt,
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
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("✅ Retrieved {Count} pending mentors", mentors.Count);

            return Result<List<PendingMentorDto>>.Success(mentors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error retrieving pending mentors");
            return Result<List<PendingMentorDto>>.Failure("Failed to retrieve pending mentors");
        }
    }
}