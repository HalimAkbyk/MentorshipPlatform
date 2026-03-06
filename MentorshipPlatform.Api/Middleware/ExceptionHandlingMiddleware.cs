
using FluentValidation;
using MentorshipPlatform.Application.Common.Behaviours;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred");
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (FeatureDisabledException ex)
        {
            _logger.LogWarning(ex, "Feature disabled: {FeatureFlag}", ex.FeatureFlagKey);
            await HandleFeatureDisabledExceptionAsync(context, ex);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception occurred");
            await HandleDomainExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var response = new
        {
            title = "Validation error",
            status = 400,
            errors
        };

        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task HandleFeatureDisabledExceptionAsync(HttpContext context, FeatureDisabledException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var response = new
        {
            title = "Feature disabled",
            status = 403,
            detail = exception.Message,
            featureFlag = exception.FeatureFlagKey
        };

        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task HandleDomainExceptionAsync(HttpContext context, DomainException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var response = new
        {
            title = "Business rule violation",
            status = 400,
            detail = exception.Message
        };

        return context.Response.WriteAsJsonAsync(response);
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Her ortamda hata tipini ve mesajını göster (debug kolaylığı için)
        // Stack trace sadece Development'ta gösterilir
        var response = new
        {
            title = "An error occurred",
            status = 500,
            detail = $"{exception.GetType().Name}: {exception.Message}",
            stackTrace = _env.IsDevelopment() ? exception.StackTrace : null
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}