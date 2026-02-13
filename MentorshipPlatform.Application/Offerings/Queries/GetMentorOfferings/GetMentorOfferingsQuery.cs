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
    Guid? AvailabilityTemplateId,
    List<MentorOfferingQuestionDto> Questions);

public record MentorOfferingQuestionDto(Guid Id, string QuestionText, bool IsRequired, int SortOrder);

public record GetMentorOfferingsQuery(Guid MentorUserId) : IRequest<Result<List<MentorOfferingDto>>>;

public class GetMentorOfferingsQueryHandler : IRequestHandler<GetMentorOfferingsQuery, Result<List<MentorOfferingDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMentorOfferingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<MentorOfferingDto>>> Handle(GetMentorOfferingsQuery request, CancellationToken ct)
    {
        // Sadece aktif ve listelenmiş mentorların aktif paketlerini göster
        var mentorExists = await _context.MentorProfiles
            .AsNoTracking()
            .AnyAsync(m => m.UserId == request.MentorUserId && m.IsListed, ct);

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
                o.AvailabilityTemplateId,
                o.Questions.OrderBy(q => q.SortOrder).Select(q =>
                    new MentorOfferingQuestionDto(q.Id, q.QuestionText, q.IsRequired, q.SortOrder)
                ).ToList()))
            .ToListAsync(ct);

        return Result<List<MentorOfferingDto>>.Success(offerings);
    }
}
