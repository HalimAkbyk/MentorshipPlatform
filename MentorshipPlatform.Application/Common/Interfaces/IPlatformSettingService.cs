namespace MentorshipPlatform.Application.Common.Interfaces;

/// <summary>
/// Service for reading platform settings from the database with caching.
/// Settings are key-value strings stored in the PlatformSettings table.
/// </summary>
public interface IPlatformSettingService
{
    /// <summary>Get a setting value by key. Returns defaultValue if not found.</summary>
    Task<string> GetAsync(string key, string defaultValue = "", CancellationToken ct = default);

    /// <summary>Get a setting value as decimal.</summary>
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m, CancellationToken ct = default);

    /// <summary>Get a setting value as int.</summary>
    Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken ct = default);

    /// <summary>Get a setting value as bool.</summary>
    Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default);

    /// <summary>Get all settings as dictionary (for public endpoint).</summary>
    Task<Dictionary<string, string>> GetAllPublicAsync(CancellationToken ct = default);

    /// <summary>Invalidate the cache (call after admin updates a setting).</summary>
    void InvalidateCache();
}

/// <summary>
/// Well-known platform setting keys.
/// </summary>
public static class PlatformSettings
{
    // ── General ──
    public const string PlatformName = "platform_name";
    public const string PlatformDescription = "platform_description";
    public const string SupportEmail = "support_email";
    public const string FrontendUrl = "frontend_url";

    // ── Fee / Commission ──
    public const string PlatformCommissionRate = "platform_commission_rate";
    public const string MentorCommissionRate = "mentor_commission_rate";
    public const string CourseCommissionRate = "course_commission_rate";

    // ── Email (SMTP) ──
    public const string SmtpHost = "smtp_host";
    public const string SmtpPort = "smtp_port";
    public const string SmtpUsername = "smtp_username";
    public const string SmtpPassword = "smtp_password";
    public const string SmtpFromEmail = "smtp_from_email";
    public const string SmtpFromName = "smtp_from_name";

    // ── Email (Provider) ──
    public const string EmailProvider = "email_provider";
    public const string ResendApiKey = "resend_api_key";
    public const string ResendFromEmail = "resend_from_email";
    public const string ResendFromName = "resend_from_name";

    // ── SMS ──
    public const string SmsEnabled = "sms_enabled";

    // ── Payment ──
    public const string PaymentProvider = "payment_provider";

    // ── Limits ──
    public const string MaxBookingPerDay = "max_booking_per_day";
    public const string MaxClassCapacity = "max_class_capacity";
    public const string DefaultSessionDurationMin = "default_session_duration_min";
    public const string BookingAutoExpireMinutes = "booking_auto_expire_minutes";

    // ── Payout ──
    public const string MinimumPayoutAmount = "minimum_payout_amount";

    // ── Dev / Debug ──
    public const string DevModeSessionBypass = "dev_mode_session_bypass";
    public const string SessionEarlyJoinMinutes = "session_early_join_minutes";

    // ── Session Lifecycle ──
    public const string SessionGracePeriodMinutes = "session_grace_period_minutes";
    public const string GroupClassGracePeriodMinutes = "group_class_grace_period_minutes";
    public const string NoShowWaitMinutes = "noshow_wait_minutes";

    // ── Keys that are sensitive (should be masked in public endpoint) ──
    public static readonly HashSet<string> SensitiveKeys = new()
    {
        SmtpPassword,
        SmtpUsername,
        ResendApiKey,
        "iyzico_api_key",
        "iyzico_secret_key",
        "twilio_auth_token",
    };
}
