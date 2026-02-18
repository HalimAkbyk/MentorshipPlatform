namespace MentorshipPlatform.Application.Common.Interfaces;

/// <summary>
/// Service for checking feature flag status. Implementations should cache results.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>Check if a feature flag is enabled.</summary>
    Task<bool> IsEnabledAsync(string key, CancellationToken ct = default);

    /// <summary>Get all feature flags as a dictionary.</summary>
    Task<Dictionary<string, bool>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Invalidate the cache (call after admin updates a flag).</summary>
    void InvalidateCache();
}

/// <summary>
/// Well-known feature flag keys.
/// </summary>
public static class FeatureFlags
{
    public const string RegistrationEnabled = "registration_enabled";
    public const string CourseSalesEnabled = "course_sales_enabled";
    public const string GroupClassesEnabled = "group_classes_enabled";
    public const string ChatEnabled = "chat_enabled";
    public const string VideoEnabled = "video_enabled";
    public const string MaintenanceMode = "maintenance_mode";
}
