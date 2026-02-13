using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Queries.GetAvailabilityTemplate;

// ---- Response DTOs ----
public record AvailabilityTemplateDto(
    Guid Id,
    string Name,
    string Timezone,
    bool IsDefault,
    AvailabilitySettingsResponseDto Settings,
    List<AvailabilityRuleResponseDto> Rules,
    List<AvailabilityOverrideResponseDto> Overrides);

public record AvailabilitySettingsResponseDto(
    int MinNoticeHours,
    int MaxBookingDaysAhead,
    int BufferAfterMin,
    int SlotGranularityMin,
    int MaxBookingsPerDay);

public record AvailabilityRuleResponseDto(
    Guid Id,
    int DayOfWeek,
    bool IsActive,
    string? StartTime,
    string? EndTime,
    int SlotIndex);

public record AvailabilityOverrideResponseDto(
    Guid Id,
    string Date,
    bool IsBlocked,
    string? StartTime,
    string? EndTime,
    string? Reason);

// ---- Query ----
public record GetAvailabilityTemplateQuery : IRequest<Result<AvailabilityTemplateDto?>>;

// ---- Handler ----
public class GetAvailabilityTemplateQueryHandler
    : IRequestHandler<GetAvailabilityTemplateQuery, Result<AvailabilityTemplateDto?>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAvailabilityTemplateQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<AvailabilityTemplateDto?>> Handle(
        GetAvailabilityTemplateQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<AvailabilityTemplateDto?>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        var template = await _context.AvailabilityTemplates
            .Include(t => t.Rules)
            .Include(t => t.Overrides)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.MentorUserId == mentorUserId && t.IsDefault, ct);

        if (template == null)
            return Result<AvailabilityTemplateDto?>.Success(null);

        var dto = new AvailabilityTemplateDto(
            template.Id,
            template.Name,
            template.Timezone,
            template.IsDefault,
            new AvailabilitySettingsResponseDto(
                template.MinNoticeHours,
                template.MaxBookingDaysAhead,
                template.BufferAfterMin,
                template.SlotGranularityMin,
                template.MaxBookingsPerDay),
            template.Rules.OrderBy(r => r.DayOfWeek).ThenBy(r => r.SlotIndex).Select(r =>
                new AvailabilityRuleResponseDto(
                    r.Id,
                    r.DayOfWeek,
                    r.IsActive,
                    r.StartTime?.ToString(@"hh\:mm"),
                    r.EndTime?.ToString(@"hh\:mm"),
                    r.SlotIndex)).ToList(),
            template.Overrides.OrderBy(o => o.Date).Select(o =>
                new AvailabilityOverrideResponseDto(
                    o.Id,
                    o.Date.ToString("yyyy-MM-dd"),
                    o.IsBlocked,
                    o.StartTime?.ToString(@"hh\:mm"),
                    o.EndTime?.ToString(@"hh\:mm"),
                    o.Reason)).ToList());

        return Result<AvailabilityTemplateDto?>.Success(dto);
    }
}
