using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Queries.GetOfferingById;

public record OfferingDetailDto(
    Guid Id,
    Guid MentorUserId,
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
    string? CoverImageUrl,
    List<OfferingQuestionDto> Questions);

public record OfferingQuestionDto(Guid Id, string QuestionText, bool IsRequired, int SortOrder);

public record GetOfferingByIdQuery(Guid OfferingId) : IRequest<Result<OfferingDetailDto?>>;

public class GetOfferingByIdQueryHandler : IRequestHandler<GetOfferingByIdQuery, Result<OfferingDetailDto?>>
{
    private readonly IApplicationDbContext _context;

    public GetOfferingByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OfferingDetailDto?>> Handle(GetOfferingByIdQuery request, CancellationToken ct)
    {
        var offering = await _context.Offerings
            .Include(o => o.Questions)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, ct);

        if (offering == null)
            return Result<OfferingDetailDto?>.Success(null);

        var dto = new OfferingDetailDto(
            offering.Id,
            offering.MentorUserId,
            offering.Type.ToString(),
            offering.Title,
            offering.Description,
            offering.DurationMinDefault,
            offering.PriceAmount,
            offering.Currency,
            offering.IsActive,
            offering.Category,
            offering.Subtitle,
            offering.DetailedDescription,
            offering.SessionType,
            offering.MaxBookingDaysAhead,
            offering.MinNoticeHours,
            offering.CoverImageUrl,
            offering.Questions.OrderBy(q => q.SortOrder).Select(q =>
                new OfferingQuestionDto(q.Id, q.QuestionText, q.IsRequired, q.SortOrder)
            ).ToList());

        return Result<OfferingDetailDto?>.Success(dto);
    }
}
