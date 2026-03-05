using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MiniDrive.Common.Caching;

/// <summary>
/// Redis-backed cache implementation used across the application.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly RedisCacheOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;

    public RedisCacheService(
        IConnectionMultiplexer connection,
        IOptions<RedisCacheOptions> options,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _database = connection.GetDatabase();
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    // Plan (pseudocode):
    // 1. Validate cancellation token.
    // 2. Get the Redis value for the normalized key.
    // 3. If the value is null or empty, return default(T).
    // 4. If the requested type T is string, return the Redis value as string.
    // 5. Otherwise, disambiguate JsonSerializer.Deserialize overload by passing a string:
    //    - Call value.ToString() to ensure the argument is a string.
    //    - Pass the JsonSerializerOptions explicitly.
    //    - Return the deserialized T (which may be null).
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = await _database.StringGetAsync(NormalizeKey(key)).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)value.ToString();
        }

        // Disambiguate between Deserialize(ReadOnlySpan<byte>, ...) and Deserialize(string, ...)
        // by explicitly passing a string.
        return JsonSerializer.Deserialize<T>(value.ToString(), _serializerOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = value switch
        {
            null => "null",
            string s => s,
            _ => JsonSerializer.Serialize(value, _serializerOptions)
        };

        var effectiveTtl = ttl ?? _options.DefaultTtl;
        if (effectiveTtl.HasValue)
        {
            await _database.StringSetAsync(
                NormalizeKey(key),
                payload,
                effectiveTtl.Value).ConfigureAwait(false);
        }
        else
        {
            await _database.StringSetAsync(
                NormalizeKey(key),
                payload).ConfigureAwait(false);
        }
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.KeyDeleteAsync(NormalizeKey(key)).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.KeyExistsAsync(NormalizeKey(key)).ConfigureAwait(false);
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var created = await factory(cancellationToken).ConfigureAwait(false);
        if (created is not null)
        {
            await SetAsync(key, created, ttl, cancellationToken).ConfigureAwait(false);
        }

        return created!;
    }

    private string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
        }

        return string.IsNullOrEmpty(_options.KeyPrefix)
            ? key.Trim()
            : $"{_options.KeyPrefix}{key.Trim()}";
    }
}
