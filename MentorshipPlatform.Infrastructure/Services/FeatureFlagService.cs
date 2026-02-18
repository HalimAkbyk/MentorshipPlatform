using System.Collections.Concurrent;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

/// <summary>
/// Feature flag service with in-memory cache (60s TTL).
/// Reads from database, caches results, and invalidates on admin updates.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FeatureFlagService> _logger;

    private static readonly ConcurrentDictionary<string, bool> _cache = new();
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private const int CacheTtlSeconds = 60;

    public FeatureFlagService(IApplicationDbContext context, ILogger<FeatureFlagService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string key, CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);

        if (_cache.TryGetValue(key, out var isEnabled))
            return isEnabled;

        // Flag not found → default to enabled (safe fallback)
        _logger.LogWarning("Feature flag '{Key}' not found, defaulting to enabled", key);
        return true;
    }

    public async Task<Dictionary<string, bool>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);
        return new Dictionary<string, bool>(_cache);
    }

    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
        _cache.Clear();
        _logger.LogInformation("Feature flag cache invalidated");
    }

    private async Task EnsureCacheAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow < _cacheExpiry && !_cache.IsEmpty)
            return;

        await _semaphore.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (DateTime.UtcNow < _cacheExpiry && !_cache.IsEmpty)
                return;

            var flags = await _context.FeatureFlags
                .AsNoTracking()
                .ToListAsync(ct);

            _cache.Clear();
            foreach (var flag in flags)
            {
                _cache[flag.Key] = flag.IsEnabled;
            }

            _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheTtlSeconds);
            _logger.LogDebug("Feature flag cache refreshed with {Count} flags", flags.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
