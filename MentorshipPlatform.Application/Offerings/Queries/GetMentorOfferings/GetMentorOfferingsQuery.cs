using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Queries.GetMentorOfferings;

public record MentorOfferingDto(
    Guid Id,
    string Type,
    string Title,
    string? Description,
    int DurationMin,
    decimal Price,
    string Currency,
    string? Category,
    string? Subtitle,
    string? DetailedDescription,
    string? SessionType,
    int MaxBookingDaysAhead,
    int MinNoticeHours,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    Guid? AvailabilityTemplateId,
    List<MentorOfferingQuestionDto> Questions);

public record MentorOfferingQuestionDto(Guid Id, string QuestionText, bool IsRequired, int SortOrder);

public record GetMentorOfferingsQuery(Guid MentorUserId) : IRequest<Result<List<MentorOfferingDto>>>;

public class GetMentorOfferingsQueryHandler : IRequestHandler<GetMentorOfferingsQuery, Result<List<MentorOfferingDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorOfferingsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<MentorOfferingDto>>> Handle(GetMentorOfferingsQuery request, CancellationToken ct)
    {
        // Kendi profilini görüntüleyen mentor IsListed kontrolünden muaf
        var isOwnProfile = _currentUser.UserId.HasValue && _currentUser.UserId.Value == request.MentorUserId;

        var mentorExists = await _context.MentorProfiles
            .AsNoTracking()
            .AnyAsync(m => m.UserId == request.MentorUserId && (m.IsListed || isOwnProfile), ct);

        if (!mentorExists)
            return Result<List<MentorOfferingDto>>.Failure("Mentor not found");

        var offerings = await _context.Offerings
            .Include(o => o.Questions)
            .AsNoTracking()
            .Where(o => o.MentorUserId == request.MentorUserId && o.IsActive)
            .OrderBy(o => o.SortOrder)
            .Select(o => new MentorOfferingDto(
                o.Id,
                o.Type.ToString(),
                o.Title,
                o.Description,
                o.DurationMinDefault,
                o.PriceAmount,
                o.Currency,
                o.Category,
                o.Subtitle,
                o.DetailedDescription,
                o.SessionType,
                o.MaxBookingDaysAhead,
                o.MinNoticeHours,
                o.CoverImageUrl,
                o.CoverImagePosition,
                o.CoverImageTransform,
                o.AvailabilityTemplateId,
                o.Questions.OrderBy(q => q.SortOrder).Select(q =>
                    new MentorOfferingQuestionDto(q.Id, q.QuestionText, q.IsRequired, q.SortOrder)
                ).ToList()))
            .ToListAsync(ct);

        return Result<List<MentorOfferingDto>>.Success(offerings);
    }
}
