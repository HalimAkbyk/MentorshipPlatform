using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Queries.GetOfferingAvailabilityTemplate;

public record OfferingAvailabilityTemplateDto(
    Guid TemplateId,
    string Name,
    string Timezone,
    bool IsDefault,
    bool HasCustomSchedule,
    OfferingTemplateSettingsDto Settings,
    List<OfferingTemplateRuleDto> Rules,
    List<OfferingTemplateOverrideDto> Overrides);

public record OfferingTemplateSettingsDto(
    int MinNoticeHours,
    int MaxBookingDaysAhead,
    int BufferAfterMin,
    int SlotGranularityMin,
    int MaxBookingsPerDay);

public record OfferingTemplateRuleDto(
    Guid Id,
    int DayOfWeek,
    bool IsActive,
    string? StartTime,
    string? EndTime,
    int SlotIndex);

public record OfferingTemplateOverrideDto(
    Guid Id,
    string Date,
    bool IsBlocked,
    string? StartTime,
    string? EndTime,
    string? Reason);

public record GetOfferingAvailabilityTemplateQuery(Guid OfferingId) : IRequest<Result<OfferingAvailabilityTemplateDto>>;

public class GetOfferingAvailabilityTemplateQueryHandler
    : IRequestHandler<GetOfferingAvailabilityTemplateQuery, Result<OfferingAvailabilityTemplateDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOfferingAvailabilityTemplateQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OfferingAvailabilityTemplateDto>> Handle(
        GetOfferingAvailabilityTemplateQuery request,
        CancellationToken ct)
    {
        var offering = await _context.Offerings
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, ct);

        if (offering == null)
            return Result<OfferingAvailabilityTemplateDto>.Failure("Offering not found");

        // Offering'e bağlı template veya default template
        Domain.Entities.AvailabilityTemplate? template = null;
        bool hasCustomSchedule = false;

        if (offering.AvailabilityTemplateId.HasValue)
        {
            template = await _context.AvailabilityTemplates
                .Include(t => t.Rules)
                .Include(t => t.Overrides)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == offering.AvailabilityTemplateId.Value, ct);
            hasCustomSchedule = template != null;
        }

        if (template == null)
        {
            template = await _context.AvailabilityTemplates
                .Include(t => t.Rules)
                .Include(t => t.Overrides)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.MentorUserId == offering.MentorUserId && t.IsDefault, ct);
        }

        if (template == null)
            return Result<OfferingAvailabilityTemplateDto>.Failure("No availability template found");

        var dto = new OfferingAvailabilityTemplateDto(
            template.Id,
            template.Name,
            template.Timezone,
            template.IsDefault,
            hasCustomSchedule,
            new OfferingTemplateSettingsDto(
                template.MinNoticeHours,
                template.MaxBookingDaysAhead,
                template.BufferAfterMin,
                template.SlotGranularityMin,
                template.MaxBookingsPerDay),
            template.Rules.OrderBy(r => r.DayOfWeek).ThenBy(r => r.SlotIndex).Select(r =>
                new OfferingTemplateRuleDto(
                    r.Id,
                    r.DayOfWeek,
                    r.IsActive,
                    r.StartTime?.ToString(@"hh\:mm"),
                    r.EndTime?.ToString(@"hh\:mm"),
                    r.SlotIndex)
            ).ToList(),
            template.Overrides.OrderBy(o => o.Date).Select(o =>
                new OfferingTemplateOverrideDto(
                    o.Id,
                    o.Date.ToString("yyyy-MM-dd"),
                    o.IsBlocked,
                    o.StartTime?.ToString(@"hh\:mm"),
                    o.EndTime?.ToString(@"hh\:mm"),
                    o.Reason)
            ).ToList());

        return Result<OfferingAvailabilityTemplateDto>.Success(dto);
    }
}
