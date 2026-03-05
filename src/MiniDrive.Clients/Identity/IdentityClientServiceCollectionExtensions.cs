using MiniDrive.Common;
using MiniDrive.Common.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MiniDrive.Clients.Identity;

/// <summary>
/// Extension methods for registering Identity client with caching.
/// </summary>
public static class IdentityClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds Identity client with token validation caching.
    /// Cache TTL: 5 minutes
    /// Requires ICacheService to be registered first.
    /// </summary>
    public static IHttpClientBuilder AddCachedIdentityClient(
        this IServiceCollection services,
        Uri baseAddress,
        TimeSpan? timeout = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));

        // Register the underlying HTTP client for IdentityClient
        var builder = services.AddHttpClient<IdentityClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
        })
        .AddDefaultResilience();

        // Register IIdentityClient as CachedIdentityClient wrapper
        // This must be done after AddHttpClient for IdentityClient
        services.AddScoped<IIdentityClient>(sp =>
        {
            var innerClient = sp.GetRequiredService<IdentityClient>();
            var cache = sp.GetRequiredService<ICacheService>();
            return new CachedIdentityClient(innerClient, cache);
        });

        return builder;
    }

    /// <summary>
    /// Adds Identity client with token validation caching.
    /// Reads baseAddress from configuration.
    /// </summary>
    public static IHttpClientBuilder AddCachedIdentityClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string configKey = "Services:Identity",
        string defaultUrl = "http://localhost:5001")
    {
        var identityServiceUrl = configuration[configKey] ?? defaultUrl;
        return services.AddCachedIdentityClient(new Uri(identityServiceUrl));
    }
}
