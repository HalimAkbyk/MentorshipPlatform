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
    // Existing flags
    public const string RegistrationEnabled = "registration_enabled";
    public const string CourseSalesEnabled = "course_sales_enabled";
    public const string GroupClassesEnabled = "group_classes_enabled";
    public const string ChatEnabled = "chat_enabled";
    public const string VideoEnabled = "video_enabled";
    public const string MaintenanceMode = "maintenance_mode";

    // Pivot flags — dikey egitim modeli
    public const string MarketplaceMode = "MARKETPLACE_MODE";
    public const string ExternalMentorRegistration = "EXTERNAL_MENTOR_REGISTRATION";
    public const string MentorSelfCourseCreation = "MENTOR_SELF_COURSE_CREATION";
    public const string MultiCategoryMode = "MULTI_CATEGORY_MODE";
    public const string CommissionPaymentModel = "COMMISSION_PAYMENT_MODEL";
    public const string PackageSystemEnabled = "PACKAGE_SYSTEM_ENABLED";
    public const string PrivateLessonEnabled = "PRIVATE_LESSON_ENABLED";
    public const string InstructorSelfScheduling = "INSTRUCTOR_SELF_SCHEDULING";
    public const string InstructorPerformanceTracking = "INSTRUCTOR_PERFORMANCE_TRACKING";
    public const string InstructorPerformanceSelfView = "INSTRUCTOR_PERFORMANCE_SELF_VIEW";
    public const string InstructorAccrualSelfView = "INSTRUCTOR_ACCRUAL_SELF_VIEW";
    public const string InstructorComparisonReport = "INSTRUCTOR_COMPARISON_REPORT";
}
