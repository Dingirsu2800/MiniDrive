using MiniDrive.Common.Caching;

namespace MiniDrive.Clients.Identity;

/// <summary>
/// Identity client wrapper that caches token validation results.
/// Reduces load on Identity service by caching validated tokens.
/// </summary>
public class CachedIdentityClient : IIdentityClient
{
    private readonly IIdentityClient _inner;
    private readonly ICacheService _cache;
    private const string CacheKeyPrefix = "identity:token:";
    private const int CacheTtlMinutes = 5;

    public CachedIdentityClient(IIdentityClient inner, ICacheService cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<UserInfo?> ValidateSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Create cache key from token hash to avoid storing the token as-is
        var tokenHash = GetTokenHash(token);
        var cacheKey = $"{CacheKeyPrefix}{tokenHash}";

        // Try to get from cache first
        var cached = await _cache.GetAsync<UserInfo>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Validate with underlying service
        var user = await _inner.ValidateSessionAsync(token, cancellationToken);
        
        if (user != null)
        {
            // Cache successful validation for TTL period
            await _cache.SetAsync(
                cacheKey,
                user,
                TimeSpan.FromMinutes(CacheTtlMinutes),
                cancellationToken);
        }

        return user;
    }

    /// <summary>
    /// Calculates hash of token for cache key.
    /// Uses SHA256 for security - doesn't store raw token.
    /// </summary>
    private static string GetTokenHash(string token)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        return System.Convert.ToBase64String(hashBytes);
    }
}
