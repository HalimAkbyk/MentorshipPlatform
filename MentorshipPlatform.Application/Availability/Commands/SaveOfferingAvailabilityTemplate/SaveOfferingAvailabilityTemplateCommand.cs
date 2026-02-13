using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Availability.Commands.SaveAvailabilityTemplate;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Commands.SaveOfferingAvailabilityTemplate;

public record SaveOfferingAvailabilityTemplateCommand(
    Guid OfferingId,
    string? Name,
    string? Timezone,
    List<AvailabilityRuleDto> Rules,
    AvailabilitySettingsDto? Settings) : IRequest<Result<Guid>>;

public class SaveOfferingAvailabilityTemplateCommandValidator
    : AbstractValidator<SaveOfferingAvailabilityTemplateCommand>
{
    public SaveOfferingAvailabilityTemplateCommandValidator()
    {
        RuleFor(x => x.OfferingId).NotEmpty();
        RuleFor(x => x.Rules).NotEmpty().WithMessage("At least one rule is required");

        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.DayOfWeek).InclusiveBetween(0, 6);
            rule.When(r => r.IsActive, () =>
            {
                rule.RuleFor(r => r.StartTime).NotEmpty();
                rule.RuleFor(r => r.EndTime).NotEmpty();
            });
        });
    }
}

public class SaveOfferingAvailabilityTemplateCommandHandler
    : IRequestHandler<SaveOfferingAvailabilityTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveOfferingAvailabilityTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        SaveOfferingAvailabilityTemplateCommand request,
        CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        // Offering'i doğrula
        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.MentorUserId == mentorUserId, ct);

        if (offering == null)
            return Result<Guid>.Failure("Offering not found");

        // Offering'e bağlı mevcut template var mı?
        AvailabilityTemplate? template = null;
        if (offering.AvailabilityTemplateId.HasValue)
        {
            template = await _context.AvailabilityTemplates
                .Include(t => t.Rules)
                .Include(t => t.Overrides)
                .FirstOrDefaultAsync(t => t.Id == offering.AvailabilityTemplateId.Value, ct);
        }

        if (template == null)
        {
            // Yeni non-default template oluştur
            template = AvailabilityTemplate.Create(
                mentorUserId,
                request.Name ?? $"{offering.Title} Programı",
                request.Timezone,
                isDefault: false);
            _context.AvailabilityTemplates.Add(template);

            // Offering'e bağla
            offering.SetAvailabilityTemplate(template.Id);
        }
        else
        {
            if (request.Name != null) template.UpdateName(request.Name);
            if (request.Timezone != null) template.UpdateTimezone(request.Timezone);
        }

        // Settings güncelle
        if (request.Settings != null)
        {
            template.UpdateSettings(
                request.Settings.MinNoticeHours,
                request.Settings.MaxBookingDaysAhead,
                request.Settings.BufferAfterMin,
                request.Settings.SlotGranularityMin,
                request.Settings.MaxBookingsPerDay);
        }

        await _context.SaveChangesAsync(ct);

        // Mevcut rules'ları sil
        var existingRules = await _context.AvailabilityRules
            .Where(r => r.TemplateId == template.Id)
            .ToListAsync(ct);

        if (existingRules.Any())
        {
            _context.AvailabilityRules.RemoveRange(existingRules);
            await _context.SaveChangesAsync(ct);
        }

        // Yeni rules oluştur
        var newRules = request.Rules.Select(r =>
            AvailabilityRule.Create(
                r.DayOfWeek,
                r.IsActive,
                r.IsActive && r.StartTime != null ? TimeSpan.Parse(r.StartTime) : null,
                r.IsActive && r.EndTime != null ? TimeSpan.Parse(r.EndTime) : null,
                r.SlotIndex)
        ).ToList();

        foreach (var rule in newRules)
        {
            rule.SetTemplateId(template.Id);
        }
        _context.AvailabilityRules.AddRange(newRules);
        await _context.SaveChangesAsync(ct);

        // Slot'ları oluştur (sadece bu template için)
        await GenerateSlotsFromTemplate(template, newRules, ct);

        return Result<Guid>.Success(template.Id);
    }

    private static TimeZoneInfo FindTimezone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Europe/Istanbul"] = "Turkey Standard Time",
                ["Turkey Standard Time"] = "Europe/Istanbul",
            };
            if (mapping.TryGetValue(timezone, out var alt))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(alt); }
                catch { /* fall through */ }
            }
            return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Turkey", "Turkey Standard Time");
        }
    }

    private static DateTime ConvertToUtcSafe(DateTime dateTime, TimeZoneInfo tz)
    {
        var dt = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(dt, tz);
    }

    private async Task GenerateSlotsFromTemplate(AvailabilityTemplate template, List<AvailabilityRule> activeRules, CancellationToken ct)
    {
        var mentorUserId = template.MentorUserId;
        var tz = FindTimezone(template.Timezone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var startDate = DateOnly.FromDateTime(now.Date);
        var endDate = startDate.AddDays(template.MaxBookingDaysAhead);

        // Sadece BU template'e ait slot'ları filtrele
        var existingSlots = await _context.AvailabilitySlots
            .Where(s => s.MentorUserId == mentorUserId
                     && s.StartAt >= DateTime.UtcNow
                     && s.TemplateId == template.Id)
            .ToListAsync(ct);

        var bookedSlots = existingSlots.Where(s => s.IsBooked).ToList();
        var unbookedSlots = existingSlots.Where(s => !s.IsBooked).ToList();

        _context.AvailabilitySlots.RemoveRange(unbookedSlots);

        var overridesList = await _context.AvailabilityOverrides
            .AsNoTracking()
            .Where(o => o.TemplateId == template.Id)
            .ToListAsync(ct);
        var overrides = overridesList
            .GroupBy(o => o.Date)
            .ToDictionary(g => g.Key, g => g.First());

        var rulesByDay = activeRules
            .Where(r => r.IsActive)
            .GroupBy(r => r.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.StartTime).ToList());

        var newSlots = new List<AvailabilitySlot>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (overrides.TryGetValue(date, out var @override))
            {
                if (@override.IsBlocked) continue;

                if (@override.StartTime.HasValue && @override.EndTime.HasValue)
                {
                    var overrideStart = date.ToDateTime(TimeOnly.FromTimeSpan(@override.StartTime.Value));
                    var overrideEnd = date.ToDateTime(TimeOnly.FromTimeSpan(@override.EndTime.Value));

                    var overrideStartUtc = ConvertToUtcSafe(overrideStart, tz);
                    var overrideEndUtc = ConvertToUtcSafe(overrideEnd, tz);

                    if (overrideStartUtc > DateTime.UtcNow &&
                        !bookedSlots.Any(b => b.StartAt < overrideEndUtc && b.EndAt > overrideStartUtc))
                    {
                        newSlots.Add(AvailabilitySlot.Create(mentorUserId, overrideStartUtc, overrideEndUtc, template.Id));
                    }
                }
                continue;
            }

            var dayOfWeek = (int)date.DayOfWeek;
            if (!rulesByDay.TryGetValue(dayOfWeek, out var rules)) continue;

            foreach (var rule in rules)
            {
                if (!rule.StartTime.HasValue || !rule.EndTime.HasValue) continue;

                var localStart = date.ToDateTime(TimeOnly.FromTimeSpan(rule.StartTime.Value));
                var localEnd = date.ToDateTime(TimeOnly.FromTimeSpan(rule.EndTime.Value));

                var utcStart = ConvertToUtcSafe(localStart, tz);
                var utcEnd = ConvertToUtcSafe(localEnd, tz);

                if (utcEnd <= DateTime.UtcNow) continue;
                if (utcStart < DateTime.UtcNow) utcStart = DateTime.UtcNow;

                if (bookedSlots.Any(b => b.StartAt < utcEnd && b.EndAt > utcStart)) continue;

                newSlots.Add(AvailabilitySlot.Create(mentorUserId, utcStart, utcEnd, template.Id));
            }
        }

        if (newSlots.Any())
        {
            _context.AvailabilitySlots.AddRange(newSlots);
        }

        await _context.SaveChangesAsync(ct);
    }
}
