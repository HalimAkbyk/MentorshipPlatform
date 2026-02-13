using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Commands.SaveAvailabilityTemplate;

// ---- DTOs ----
public record AvailabilityRuleDto(int DayOfWeek, bool IsActive, string? StartTime, string? EndTime, int SlotIndex = 0);
public record AvailabilitySettingsDto(
    int? MinNoticeHours,
    int? MaxBookingDaysAhead,
    int? BufferAfterMin,
    int? SlotGranularityMin,
    int? MaxBookingsPerDay);

// ---- Command ----
public record SaveAvailabilityTemplateCommand(
    string? Name,
    string? Timezone,
    List<AvailabilityRuleDto> Rules,
    AvailabilitySettingsDto? Settings) : IRequest<Result<Guid>>;

// ---- Validator ----
public class SaveAvailabilityTemplateCommandValidator : AbstractValidator<SaveAvailabilityTemplateCommand>
{
    public SaveAvailabilityTemplateCommandValidator()
    {
        RuleFor(x => x.Rules)
            .NotEmpty().WithMessage("At least one rule is required");

        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.DayOfWeek)
                .InclusiveBetween(0, 6).WithMessage("DayOfWeek must be 0-6");

            rule.When(r => r.IsActive, () =>
            {
                rule.RuleFor(r => r.StartTime)
                    .NotEmpty().WithMessage("Active rules must have a start time");
                rule.RuleFor(r => r.EndTime)
                    .NotEmpty().WithMessage("Active rules must have an end time");
            });
        });
    }
}

// ---- Handler ----
public class SaveAvailabilityTemplateCommandHandler
    : IRequestHandler<SaveAvailabilityTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveAvailabilityTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        SaveAvailabilityTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        // Mevcut template'i bul veya yeni oluştur
        var template = await _context.AvailabilityTemplates
            .Include(t => t.Rules)
            .Include(t => t.Overrides)
            .FirstOrDefaultAsync(t => t.MentorUserId == mentorUserId && t.IsDefault, cancellationToken);

        if (template == null)
        {
            template = AvailabilityTemplate.Create(mentorUserId, request.Name, request.Timezone);
            _context.AvailabilityTemplates.Add(template);
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

        // Rules güncelle
        // Not: template Include ile yüklendiğinde, Rules collection'ı EF Core
        // tarafından track ediliyor. Doğrudan RemoveRange + SetRules karışıklık
        // yaratıyor. Bunun yerine, önce mevcut template + rules kaydedip,
        // sonra rules'ları ayrıca temizleyip ekliyoruz.

        // Önce template'i ve settings'i kaydet
        await _context.SaveChangesAsync(cancellationToken);

        // Şimdi mevcut rules'ları DB'den sil (ayrı sorgu ile)
        var existingRuleIds = await _context.AvailabilityRules
            .Where(r => r.TemplateId == template.Id)
            .ToListAsync(cancellationToken);

        if (existingRuleIds.Any())
        {
            _context.AvailabilityRules.RemoveRange(existingRuleIds);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Yeni rules oluştur ve doğrudan DbSet'e ekle
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
        await _context.SaveChangesAsync(cancellationToken);

        // Template kaydedildikten sonra slotları otomatik oluştur
        // Rules'ları direkt parametre olarak geçir (navigation property güncellenmemiş olabilir)
        await GenerateSlotsFromTemplate(template, newRules, cancellationToken);

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
            // Docker Alpine may not have all timezone data, try common mappings
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
            // Fallback: UTC+3 for Turkey
            return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Turkey", "Turkey Standard Time");
        }
    }

    private static DateTime ConvertToUtcSafe(DateTime dateTime, TimeZoneInfo tz)
    {
        // Ensure DateTimeKind is Unspecified for ConvertTimeToUtc
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

        // Booked olan mevcut slotları koru, booked olmayanları sil ve yeniden oluştur
        var existingSlots = await _context.AvailabilitySlots
            .Where(s => s.MentorUserId == mentorUserId && s.StartAt >= DateTime.UtcNow)
            .ToListAsync(ct);

        var bookedSlots = existingSlots.Where(s => s.IsBooked).ToList();
        var unbookedSlots = existingSlots.Where(s => !s.IsBooked).ToList();

        // Booked olmayan gelecek slotları sil
        _context.AvailabilitySlots.RemoveRange(unbookedSlots);

        // Overrides'ı DB'den doğrudan oku (navigation property güvenilmez olabilir)
        var overridesList = await _context.AvailabilityOverrides
            .AsNoTracking()
            .Where(o => o.TemplateId == template.Id)
            .ToListAsync(ct);
        var overrides = overridesList
            .GroupBy(o => o.Date)
            .ToDictionary(g => g.Key, g => g.First());

        // Rules parametre olarak geliyor, tekrar sorgulamaya gerek yok
        var rulesByDay = activeRules
            .Where(r => r.IsActive)
            .GroupBy(r => r.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.StartTime).ToList());

        var newSlots = new List<AvailabilitySlot>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Override kontrolü
            if (overrides.TryGetValue(date, out var @override))
            {
                if (@override.IsBlocked) continue; // Gün tamamen kapalı

                // Özel saat: override saatlerini kullan
                if (@override.StartTime.HasValue && @override.EndTime.HasValue)
                {
                    var overrideStart = date.ToDateTime(TimeOnly.FromTimeSpan(@override.StartTime.Value));
                    var overrideEnd = date.ToDateTime(TimeOnly.FromTimeSpan(@override.EndTime.Value));

                    var overrideStartUtc = ConvertToUtcSafe(overrideStart, tz);
                    var overrideEndUtc = ConvertToUtcSafe(overrideEnd, tz);

                    if (overrideStartUtc > DateTime.UtcNow &&
                        !bookedSlots.Any(b => b.StartAt < overrideEndUtc && b.EndAt > overrideStartUtc))
                    {
                        newSlots.Add(AvailabilitySlot.Create(mentorUserId, overrideStartUtc, overrideEndUtc));
                    }
                }
                continue;
            }

            // Normal haftalık kural
            var dayOfWeek = (int)date.DayOfWeek;
            if (!rulesByDay.TryGetValue(dayOfWeek, out var rules)) continue;

            foreach (var rule in rules)
            {
                if (!rule.StartTime.HasValue || !rule.EndTime.HasValue) continue;

                var localStart = date.ToDateTime(TimeOnly.FromTimeSpan(rule.StartTime.Value));
                var localEnd = date.ToDateTime(TimeOnly.FromTimeSpan(rule.EndTime.Value));

                var utcStart = ConvertToUtcSafe(localStart, tz);
                var utcEnd = ConvertToUtcSafe(localEnd, tz);

                // Geçmiş slotları atla
                if (utcEnd <= DateTime.UtcNow) continue;
                if (utcStart < DateTime.UtcNow) utcStart = DateTime.UtcNow;

                // Booked slotlarla çakışma kontrolü
                if (bookedSlots.Any(b => b.StartAt < utcEnd && b.EndAt > utcStart)) continue;

                newSlots.Add(AvailabilitySlot.Create(mentorUserId, utcStart, utcEnd));
            }
        }

        if (newSlots.Any())
        {
            _context.AvailabilitySlots.AddRange(newSlots);
        }

        await _context.SaveChangesAsync(ct);
    }
}
