using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

/// <summary>
/// Public endpoint to get current feature flag statuses.
/// No authentication required — frontend needs this to conditionally render UI.
/// </summary>
[ApiController]
[Route("api/feature-flags")]
[AllowAnonymous]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureFlagsController(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    /// <summary>
    /// Get all feature flags as key-value pairs.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Dictionary<string, bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var flags = await _featureFlagService.GetAllAsync(ct);
        return Ok(flags);
    }
}
