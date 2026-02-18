using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

/// <summary>
/// Public endpoint to get current platform settings (non-sensitive).
/// No authentication required — frontend needs these for display purposes.
/// </summary>
[ApiController]
[Route("api/platform-settings")]
[AllowAnonymous]
public class PlatformSettingsController : ControllerBase
{
    private readonly IPlatformSettingService _settingsService;

    public PlatformSettingsController(IPlatformSettingService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Get all platform settings as key-value pairs (sensitive values are masked).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var settings = await _settingsService.GetAllPublicAsync(ct);
        return Ok(settings);
    }
}
