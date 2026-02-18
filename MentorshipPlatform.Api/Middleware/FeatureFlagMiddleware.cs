using MentorshipPlatform.Application.Common.Interfaces;

namespace MentorshipPlatform.Api.Middleware;

/// <summary>
/// Middleware that checks maintenance_mode feature flag.
/// When enabled, returns 503 for all non-admin, non-health requests.
/// Admin users (with RequireAdminRole claim) can still access the system.
/// </summary>
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MaintenanceModeMiddleware> _logger;

    // Paths that should always be accessible (even in maintenance mode)
    private static readonly string[] AllowedPaths =
    {
        "/health",
        "/api/feature-flags",     // Public feature flags endpoint (so frontend can detect maintenance)
        "/api/platform-settings", // Public platform settings endpoint
        "/api/auth/login",        // Admin must be able to login
        "/api/auth/me",           // Auth check
        "/api/admin/",            // All admin endpoints
        "/hangfire",
        "/hubs/",                 // SignalR hubs
        "/swagger",
    };

    public MaintenanceModeMiddleware(RequestDelegate next, ILogger<MaintenanceModeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if path is in the allowed list
        var path = context.Request.Path.Value ?? "";
        if (AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Check if user is an admin (admins bypass maintenance mode)
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole("Admin"))
        {
            await _next(context);
            return;
        }

        // Check maintenance mode flag
        var featureFlagService = context.RequestServices.GetRequiredService<IFeatureFlagService>();
        var isMaintenanceMode = await featureFlagService.IsEnabledAsync(FeatureFlags.MaintenanceMode);

        if (isMaintenanceMode)
        {
            _logger.LogInformation("Maintenance mode active. Blocking request to {Path}", path);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Bakim modu",
                status = 503,
                detail = "Sistem su anda bakim modundadir. Lutfen daha sonra tekrar deneyin."
            });
            return;
        }

        await _next(context);
    }
}

public static class MaintenanceModeMiddlewareExtensions
{
    public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MaintenanceModeMiddleware>();
    }
}
