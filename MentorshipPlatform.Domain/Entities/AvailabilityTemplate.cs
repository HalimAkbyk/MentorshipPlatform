using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Domain.Entities;

public class AvailabilityTemplate : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string Name { get; private set; } = "Varsayılan Program";
    public string Timezone { get; private set; } = "Europe/Istanbul";
    public bool IsDefault { get; private set; } = true;

    // Settings
    public int MinNoticeHours { get; private set; } = 2;
    public int MaxBookingDaysAhead { get; private set; } = 60;
    public int BufferAfterMin { get; private set; } = 15;
    public int SlotGranularityMin { get; private set; } = 30;
    public int MaxBookingsPerDay { get; private set; } = 5;

    private readonly List<AvailabilityRule> _rules = new();
    public IReadOnlyCollection<AvailabilityRule> Rules => _rules.AsReadOnly();

    private readonly List<AvailabilityOverride> _overrides = new();
    public IReadOnlyCollection<AvailabilityOverride> Overrides => _overrides.AsReadOnly();

    private AvailabilityTemplate() { }

    public static AvailabilityTemplate Create(Guid mentorUserId, string? name = null, string? timezone = null)
    {
        return new AvailabilityTemplate
        {
            MentorUserId = mentorUserId,
            Name = name ?? "Varsayılan Program",
            Timezone = timezone ?? "Europe/Istanbul",
            IsDefault = true
        };
    }

    public void UpdateSettings(int? minNoticeHours, int? maxBookingDaysAhead, int? bufferAfterMin,
                               int? slotGranularityMin, int? maxBookingsPerDay)
    {
        if (minNoticeHours.HasValue) MinNoticeHours = minNoticeHours.Value;
        if (maxBookingDaysAhead.HasValue) MaxBookingDaysAhead = maxBookingDaysAhead.Value;
        if (bufferAfterMin.HasValue) BufferAfterMin = bufferAfterMin.Value;
        if (slotGranularityMin.HasValue) SlotGranularityMin = slotGranularityMin.Value;
        if (maxBookingsPerDay.HasValue) MaxBookingsPerDay = maxBookingsPerDay.Value;
    }

    public void UpdateName(string name) => Name = name;
    public void UpdateTimezone(string timezone) => Timezone = timezone;

    public void SetRules(List<AvailabilityRule> rules)
    {
        _rules.Clear();
        foreach (var rule in rules)
        {
            rule.SetTemplateId(Id);
            _rules.Add(rule);
        }
    }

    public void AddOverride(AvailabilityOverride @override)
    {
        @override.SetTemplateId(Id);
        _overrides.Add(@override);
    }

    public void RemoveOverride(Guid overrideId)
    {
        var item = _overrides.FirstOrDefault(o => o.Id == overrideId);
        if (item != null) _overrides.Remove(item);
    }

    public void ClearOverrides() => _overrides.Clear();
}

public class AvailabilityRule : BaseEntity
{
    public Guid TemplateId { get; private set; }
    public int DayOfWeek { get; private set; } // 0=Sunday, 1=Monday, ...
    public bool IsActive { get; private set; }
    public TimeSpan? StartTime { get; private set; }
    public TimeSpan? EndTime { get; private set; }
    public int SlotIndex { get; private set; } // For multiple blocks per day (0, 1, 2...)

    private AvailabilityRule() { }

    public static AvailabilityRule Create(int dayOfWeek, bool isActive, TimeSpan? startTime, TimeSpan? endTime, int slotIndex = 0)
    {
        if (isActive && (!startTime.HasValue || !endTime.HasValue))
            throw new DomainException("Active rules must have start and end times");

        if (isActive && endTime <= startTime)
            throw new DomainException("End time must be after start time");

        return new AvailabilityRule
        {
            DayOfWeek = dayOfWeek,
            IsActive = isActive,
            StartTime = startTime,
            EndTime = endTime,
            SlotIndex = slotIndex
        };
    }

    public void SetTemplateId(Guid templateId) => TemplateId = templateId;
}

public class AvailabilityOverride : BaseEntity
{
    public Guid TemplateId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsBlocked { get; private set; }
    public TimeSpan? StartTime { get; private set; }
    public TimeSpan? EndTime { get; private set; }
    public string? Reason { get; private set; }

    private AvailabilityOverride() { }

    public static AvailabilityOverride Create(DateOnly date, bool isBlocked, TimeSpan? startTime = null,
                                               TimeSpan? endTime = null, string? reason = null)
    {
        if (!isBlocked && (!startTime.HasValue || !endTime.HasValue))
            throw new DomainException("Non-blocked overrides must have start and end times");

        return new AvailabilityOverride
        {
            Date = date,
            IsBlocked = isBlocked,
            StartTime = startTime,
            EndTime = endTime,
            Reason = reason
        };
    }

    public void SetTemplateId(Guid templateId) => TemplateId = templateId;
}
