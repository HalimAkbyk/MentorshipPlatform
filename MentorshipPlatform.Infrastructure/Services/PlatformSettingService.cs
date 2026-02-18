using System.Collections.Concurrent;
using System.Globalization;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

/// <summary>
/// Platform setting service with in-memory cache (60s TTL).
/// Reads from PlatformSettings table and caches all settings.
/// </summary>
public class PlatformSettingService : IPlatformSettingService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PlatformSettingService> _logger;

    private static readonly ConcurrentDictionary<string, string> _cache = new();
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private const int CacheTtlSeconds = 60;

    public PlatformSettingService(IApplicationDbContext context, ILogger<PlatformSettingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GetAsync(string key, string defaultValue = "", CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);
        return _cache.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m, CancellationToken ct = default)
    {
        var str = await GetAsync(key, "", ct);
        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var str = await GetAsync(key, "", ct);
        return int.TryParse(str, out var val) ? val : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var str = await GetAsync(key, "", ct);
        return bool.TryParse(str, out var val) ? val : defaultValue;
    }

    public async Task<Dictionary<string, string>> GetAllPublicAsync(CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);

        var result = new Dictionary<string, string>();
        foreach (var kvp in _cache)
        {
            // Mask sensitive values
            if (PlatformSettings.SensitiveKeys.Contains(kvp.Key))
            {
                result[kvp.Key] = string.IsNullOrEmpty(kvp.Value) ? "" : "********";
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
        _cache.Clear();
        _logger.LogInformation("Platform settings cache invalidated");
    }

    private async Task EnsureCacheAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow < _cacheExpiry && !_cache.IsEmpty)
            return;

        await _semaphore.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow < _cacheExpiry && !_cache.IsEmpty)
                return;

            var settings = await _context.PlatformSettings
                .AsNoTracking()
                .ToListAsync(ct);

            _cache.Clear();
            foreach (var s in settings)
            {
                _cache[s.Key] = s.Value;
            }

            _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheTtlSeconds);
            _logger.LogDebug("Platform settings cache refreshed with {Count} settings", settings.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
