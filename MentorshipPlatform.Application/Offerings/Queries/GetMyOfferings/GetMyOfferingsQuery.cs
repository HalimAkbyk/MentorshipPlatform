using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Queries.GetMyOfferings;

public record MyOfferingDto(
    Guid Id,
    string Type,
    string Title,
    string? Description,
    int DurationMin,
    decimal Price,
    string Currency,
    bool IsActive,
    string? Category,
    string? Subtitle,
    string? DetailedDescription,
    string? SessionType,
    int MaxBookingDaysAhead,
    int MinNoticeHours,
    int SortOrder,
    string? CoverImageUrl,
    Guid? AvailabilityTemplateId,
    int QuestionCount,
    List<MyOfferingQuestionDto> Questions);

public record MyOfferingQuestionDto(Guid Id, string QuestionText, bool IsRequired, int SortOrder);

public record GetMyOfferingsQuery : IRequest<Result<List<MyOfferingDto>>>;

public class GetMyOfferingsQueryHandler : IRequestHandler<GetMyOfferingsQuery, Result<List<MyOfferingDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyOfferingsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<MyOfferingDto>>> Handle(GetMyOfferingsQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<MyOfferingDto>>.Failure("User not authenticated");

        var offerings = await _context.Offerings
            .Include(o => o.Questions)
            .AsNoTracking()
            .Where(o => o.MentorUserId == _currentUser.UserId.Value)
            .OrderBy(o => o.SortOrder)
            .Select(o => new MyOfferingDto(
                o.Id,
                o.Type.ToString(),
                o.Title,
                o.Description,
                o.DurationMinDefault,
                o.PriceAmount,
                o.Currency,
                o.IsActive,
                o.Category,
                o.Subtitle,
                o.DetailedDescription,
                o.SessionType,
                o.MaxBookingDaysAhead,
                o.MinNoticeHours,
                o.SortOrder,
                o.CoverImageUrl,
                o.AvailabilityTemplateId,
                o.Questions.Count,
                o.Questions.OrderBy(q => q.SortOrder).Select(q =>
                    new MyOfferingQuestionDto(q.Id, q.QuestionText, q.IsRequired, q.SortOrder)
                ).ToList()))
            .ToListAsync(ct);

        return Result<List<MyOfferingDto>>.Success(offerings);
    }
}
